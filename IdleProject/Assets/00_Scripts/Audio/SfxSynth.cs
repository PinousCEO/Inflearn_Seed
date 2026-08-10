using System;
using UnityEngine;

namespace IdleBattle.Audio
{
    public enum Wave
    {
        Sine,
        Triangle,
        Saw,
        Square,
        Noise,
        /// <summary>비배음 파셜을 겹친 금속성 음색입니다. 동전·칼처럼 쇳소리가 필요할 때 씁니다.</summary>
        Metal,
        /// <summary>정수 배음만 겹친 맑은 음색입니다. 실로폰·오르골 같은 밝은 소리를 냅니다.</summary>
        Chime
    }

    /// <summary>
    /// 효과음을 코드로 합성하기 위한 최소한의 DSP 도구입니다.
    ///
    /// 레이어 하나를 float[] 한 벌로 만들고 → 필터와 엔벨로프를 건 뒤 → 최종 버퍼에 섞는 방식으로 씁니다.
    /// 오디오 파일이 프로젝트에 하나도 없어도 게임이 제 소리를 내도록 하는 것이 목적이며,
    /// Resources/Audio/{SfxId} 이름으로 실제 음원을 넣으면 <see cref="SfxLibrary"/>가 그쪽을 먼저 씁니다.
    ///
    /// 난수는 <see cref="Rng"/>로 시드를 직접 쥐고 있어, 어떤 기기에서 돌려도 같은 소리가 나옵니다.
    /// </summary>
    internal static class SfxSynth
    {
        public const int SampleRate = 44100;
        private const float TwoPi = 6.2831853f;

        /// <summary>실행·기기와 무관하게 같은 소리가 나오도록 시드를 직접 쥔 xorshift 난수입니다.</summary>
        public sealed class Rng
        {
            private uint state;

            public Rng(uint seed) => state = seed == 0u ? 2463534242u : seed;

            public float Unit() => Raw() * (1f / 4294967296f);
            public float Bipolar() => Raw() * (2f / 4294967296f) - 1f;
            public float Range(float min, float max) => min + (max - min) * Unit();

            private uint Raw()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }
        }

        public static float[] Buffer(float seconds)
            => new float[Mathf.Max(1, Mathf.CeilToInt(seconds * SampleRate))];

        // ------------------------------------------------------------------
        // 발진기
        // ------------------------------------------------------------------

        /// <summary>
        /// 한 레이어를 만듭니다. 주파수는 <paramref name="startHz"/>에서 <paramref name="endHz"/>까지
        /// <paramref name="sweepShape"/> 곡선을 따라 훑습니다(1이면 직선, &lt;1이면 앞이 빠릅니다).
        /// </summary>
        public static float[] Tone(
            float seconds,
            Wave wave,
            float startHz,
            float endHz,
            float amplitude = 1f,
            float sweepShape = 1f,
            Rng rng = null,
            float vibratoHz = 0f,
            float vibratoDepth = 0f)
        {
            var buffer = Buffer(seconds);
            rng ??= new Rng(9176u);

            // 파셜 위상을 정확히 유지해야 금속 음색에서 클릭이 생기지 않으므로 double로 누적합니다.
            var cycles = 0.0;
            var vibratoCycles = 0.0;
            var last = Mathf.Max(1, buffer.Length - 1);

            for (var i = 0; i < buffer.Length; i++)
            {
                var t = i / (float)last;
                var shaped = Mathf.Approximately(sweepShape, 1f) ? t : Mathf.Pow(t, sweepShape);
                var hz = Mathf.Lerp(startHz, endHz, shaped);

                if (vibratoDepth > 0f)
                {
                    hz *= 1f + Mathf.Sin((float)(Frac(vibratoCycles) * TwoPi)) * vibratoDepth;
                    vibratoCycles += vibratoHz / SampleRate;
                }

                buffer[i] = Osc(wave, cycles, rng) * amplitude;
                cycles += hz / SampleRate;
            }

            return buffer;
        }

        public static float[] Noise(float seconds, float amplitude = 1f, Rng rng = null)
        {
            var buffer = Buffer(seconds);
            rng ??= new Rng(4271u);
            for (var i = 0; i < buffer.Length; i++) buffer[i] = rng.Bipolar() * amplitude;
            return buffer;
        }

        private static float Osc(Wave wave, double cycles, Rng rng)
        {
            var u = Frac(cycles);
            switch (wave)
            {
                case Wave.Sine: return Mathf.Sin((float)(u * TwoPi));
                case Wave.Triangle: return 4f * Mathf.Abs((float)u - .5f) - 1f;
                case Wave.Saw: return (float)(u * 2.0 - 1.0);
                case Wave.Square: return u < .5 ? 1f : -1f;
                case Wave.Noise: return rng.Bipolar();
                case Wave.Metal:
                    // 종·검처럼 배음이 어긋난 소리를 만드는 고전적인 비정수 배음비입니다.
                    return (Mathf.Sin((float)(Frac(cycles) * TwoPi))
                            + .62f * Mathf.Sin((float)(Frac(cycles * 2.76) * TwoPi))
                            + .44f * Mathf.Sin((float)(Frac(cycles * 5.404) * TwoPi))
                            + .28f * Mathf.Sin((float)(Frac(cycles * 8.933) * TwoPi))) * .43f;
                case Wave.Chime:
                    // 정수 배음만 써서 불협이 생기지 않습니다. 밝고 동글동글한 인상을 줍니다.
                    return (Mathf.Sin((float)(Frac(cycles) * TwoPi))
                            + .38f * Mathf.Sin((float)(Frac(cycles * 2.0) * TwoPi))
                            + .18f * Mathf.Sin((float)(Frac(cycles * 3.0) * TwoPi))
                            + .08f * Mathf.Sin((float)(Frac(cycles * 4.0) * TwoPi))) * .62f;
                default: return 0f;
            }
        }

        private static double Frac(double value) => value - Math.Floor(value);

        // ------------------------------------------------------------------
        // 엔벨로프
        // ------------------------------------------------------------------

        /// <summary>타격음의 기본형입니다. 짧게 치고 지수적으로 사라집니다.</summary>
        public static float[] Pluck(float[] buffer, float attackSeconds, float decaySeconds)
        {
            var attack = Mathf.Max(1, Mathf.RoundToInt(attackSeconds * SampleRate));
            var tau = Mathf.Max(1e-4f, decaySeconds);
            for (var i = 0; i < buffer.Length; i++)
            {
                var rise = i < attack ? i / (float)attack : 1f;
                var time = Mathf.Max(0, i - attack) / (float)SampleRate;
                buffer[i] *= rise * Mathf.Exp(-time / tau);
            }
            return buffer;
        }

        /// <summary>서서히 부풀었다가 잦아드는 형태입니다. 바람·주문 지속음에 씁니다.</summary>
        public static float[] Shape(float[] buffer, float attackSeconds, float releaseSeconds)
        {
            var attack = Mathf.Max(1, Mathf.RoundToInt(attackSeconds * SampleRate));
            var release = Mathf.Max(1, Mathf.RoundToInt(releaseSeconds * SampleRate));
            var releaseStart = Mathf.Max(attack, buffer.Length - release);

            for (var i = 0; i < buffer.Length; i++)
            {
                float gain;
                if (i < attack) gain = i / (float)attack;
                else if (i >= releaseStart) gain = 1f - (i - releaseStart) / (float)Mathf.Max(1, buffer.Length - releaseStart);
                else gain = 1f;
                buffer[i] *= gain * gain;
            }
            return buffer;
        }

        /// <summary>
        /// 가장 큰 지점을 직접 정하는 엔벨로프입니다.
        /// 무기를 휘두르는 소리는 칼끝이 가장 빨라지는 후반부에 힘이 실리기 때문에,
        /// 앞뒤가 같은 <see cref="Shape"/>로는 "쉬익" 하고 지나가는 느낌이 나지 않습니다.
        /// </summary>
        /// <param name="peak">소리가 가장 커지는 지점입니다(0~1).</param>
        public static float[] Swell(float[] buffer, float peak = .7f, float rise = 2.4f, float fall = 2f)
        {
            peak = Mathf.Clamp(peak, .05f, .95f);
            var last = Mathf.Max(1, buffer.Length - 1);

            for (var i = 0; i < buffer.Length; i++)
            {
                var t = i / (float)last;
                var gain = t < peak
                    ? Mathf.Pow(t / peak, rise)
                    : Mathf.Pow(1f - (t - peak) / (1f - peak), fall);
                buffer[i] *= gain;
            }
            return buffer;
        }

        /// <summary>뒤쪽만 부드럽게 깎아 끝을 정리합니다. 클립 끝의 딸깍임을 막습니다.</summary>
        public static float[] FadeOut(float[] buffer, float seconds)
        {
            var count = Mathf.Clamp(Mathf.RoundToInt(seconds * SampleRate), 1, buffer.Length);
            var start = buffer.Length - count;
            for (var i = start; i < buffer.Length; i++)
            {
                var t = (i - start) / (float)count;
                buffer[i] *= 1f - t * t;
            }
            return buffer;
        }

        // ------------------------------------------------------------------
        // 필터
        // ------------------------------------------------------------------

        public static float[] LowPass(float[] buffer, float startHz, float endHz)
        {
            var y = 0f;
            var last = Mathf.Max(1, buffer.Length - 1);
            for (var i = 0; i < buffer.Length; i++)
            {
                var hz = Mathf.Max(20f, Mathf.Lerp(startHz, endHz, i / (float)last));
                var alpha = Coefficient(hz);
                y += alpha * (buffer[i] - y);
                buffer[i] = y;
            }
            return buffer;
        }

        public static float[] HighPass(float[] buffer, float startHz, float endHz)
        {
            var y = 0f;
            var previous = 0f;
            var last = Mathf.Max(1, buffer.Length - 1);
            for (var i = 0; i < buffer.Length; i++)
            {
                var hz = Mathf.Max(20f, Mathf.Lerp(startHz, endHz, i / (float)last));
                var alpha = 1f - Coefficient(hz);
                var input = buffer[i];
                y = alpha * (y + input - previous);
                previous = input;
                buffer[i] = y;
            }
            return buffer;
        }

        /// <summary>공진이 있는 저역통과입니다. 주문·빔처럼 훑고 지나가는 소리에 씁니다.</summary>
        public static float[] Resonant(float[] buffer, float startHz, float endHz, float q = 4f)
            => StateVariable(buffer, startHz, endHz, q, false);

        /// <summary>대역통과입니다. 바람 소리(휘두르기·씬 전환)의 기본 재료입니다.</summary>
        public static float[] BandPass(float[] buffer, float startHz, float endHz, float q = 3f)
            => StateVariable(buffer, startHz, endHz, q, true);

        private static float[] StateVariable(float[] buffer, float startHz, float endHz, float q, bool band)
        {
            var low = 0f;
            var bandPass = 0f;
            var damp = 1f / Mathf.Max(.5f, q);
            var last = Mathf.Max(1, buffer.Length - 1);

            for (var i = 0; i < buffer.Length; i++)
            {
                var hz = Mathf.Clamp(Mathf.Lerp(startHz, endHz, i / (float)last), 20f, SampleRate * .22f);
                var f = 2f * Mathf.Sin(Mathf.PI * hz / SampleRate);
                var high = buffer[i] - low - damp * bandPass;
                bandPass += f * high;
                low += f * bandPass;
                buffer[i] = band ? bandPass : low;
            }
            return buffer;
        }

        private static float Coefficient(float hz)
        {
            var dt = 1f / SampleRate;
            var rc = 1f / (TwoPi * hz);
            return dt / (rc + dt);
        }

        // ------------------------------------------------------------------
        // 종소리 (오르골 · 마림바 · 유리)
        // ------------------------------------------------------------------

        /// <summary>배음 하나의 비율 · 크기 · 사그라지는 속도입니다.</summary>
        public readonly struct Partial
        {
            public readonly float Ratio;
            public readonly float Gain;
            public readonly float Decay;

            public Partial(float ratio, float gain, float decay)
            {
                Ratio = ratio;
                Gain = gain;
                Decay = decay;
            }
        }

        /// <summary>
        /// 배음마다 사그라지는 속도를 따로 주는 가산 합성입니다.
        ///
        /// 진짜 종과 오르골은 높은 배음이 먼저 죽고 기본음만 남습니다.
        /// 그래서 처음엔 "팅" 하고 밝게 시작해 이내 맑고 둥근 소리로 풀립니다.
        /// 모든 배음에 같은 엔벨로프를 걸면 이 변화가 없어 전자음처럼 납작하게 들립니다.
        ///
        /// <paramref name="detuneCents"/>를 주면 살짝 어긋난 사본을 겹쳐 넓고 일렁이는 느낌을 더합니다.
        /// </summary>
        public static float[] Bell(
            float seconds,
            float baseHz,
            Partial[] partials,
            float attack = .006f,
            float detuneCents = 0f,
            float amplitude = 1f)
        {
            var buffer = Buffer(seconds);
            if (partials == null) return buffer;

            foreach (var partial in partials)
            {
                var hz = baseHz * partial.Ratio;
                // 표현할 수 없는 높은 배음은 버립니다. 그대로 두면 엉뚱한 저음으로 접혀 들어옵니다.
                if (hz > SampleRate * .45f) continue;

                var layer = Tone(seconds, Wave.Sine, hz, hz, partial.Gain * amplitude);
                Pluck(layer, attack, partial.Decay);
                Mix(buffer, layer);

                if (detuneCents <= 0f) continue;

                var detunedHz = hz * Mathf.Pow(2f, detuneCents / 1200f);
                var detuned = Tone(seconds, Wave.Sine, detunedHz, detunedHz, partial.Gain * amplitude * .5f);
                Pluck(detuned, attack, partial.Decay);
                Mix(buffer, detuned);
            }

            return buffer;
        }

        // ------------------------------------------------------------------
        // 이펙트
        // ------------------------------------------------------------------

        /// <summary>
        /// 넓은 공간의 울림입니다. 신비로운 인상은 대부분 여기서 나옵니다.
        ///
        /// 병렬 콤 필터 넷으로 사방에서 돌아오는 반사를 만들고,
        /// 직렬 올패스 둘로 그 반사를 흩어 성긴 메아리가 아닌 매끄러운 잔향으로 만듭니다.
        /// 반사될수록 고역이 먼저 죽도록 <paramref name="damping"/>으로 깎아, 울림이 부드럽게 잦아듭니다.
        /// </summary>
        public static float[] Reverb(float[] buffer, float mix = .3f, float size = 1f, float damping = .4f)
        {
            if (mix <= 0f) return buffer;

            var wet = new float[buffer.Length];
            damping = Mathf.Clamp01(damping);

            // 서로 나누어떨어지지 않는 길이라야 반사가 겹쳐 뭉치지 않습니다.
            foreach (var delaySeconds in new[] { .0297f, .0371f, .0411f, .0437f })
            {
                var length = Mathf.Max(1, Mathf.RoundToInt(delaySeconds * Mathf.Max(.1f, size) * SampleRate));
                var line = new float[length];
                var index = 0;
                var damped = 0f;

                for (var i = 0; i < buffer.Length; i++)
                {
                    var delayed = line[index];
                    wet[i] += delayed;
                    damped = delayed * (1f - damping) + damped * damping;
                    line[index] = buffer[i] + damped * .78f;
                    index = index + 1 >= length ? 0 : index + 1;
                }
            }

            for (var i = 0; i < wet.Length; i++) wet[i] *= .25f;

            foreach (var delaySeconds in new[] { .005f, .0017f })
            {
                var length = Mathf.Max(1, Mathf.RoundToInt(delaySeconds * SampleRate));
                var line = new float[length];
                var index = 0;
                const float feedback = .7f;

                for (var i = 0; i < wet.Length; i++)
                {
                    var delayed = line[index];
                    var input = wet[i];
                    line[index] = input + delayed * feedback;
                    wet[i] = delayed - input * feedback;
                    index = index + 1 >= length ? 0 : index + 1;
                }
            }

            for (var i = 0; i < buffer.Length; i++) buffer[i] += wet[i] * mix;
            return buffer;
        }

        /// <summary>부드럽게 찌그러뜨려 두께와 거친 질감을 더합니다.</summary>
        public static float[] Drive(float[] buffer, float amount)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var x = buffer[i] * amount;
                buffer[i] = x / (1f + Mathf.Abs(x));
            }
            return buffer;
        }

        /// <summary>짧은 피드백 딜레이로 동굴 같은 잔향 꼬리를 붙입니다.</summary>
        public static float[] Tail(float[] buffer, float delaySeconds, float feedback, float mix)
        {
            var length = Mathf.Max(1, Mathf.RoundToInt(delaySeconds * SampleRate));
            var line = new float[length];
            var index = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                var delayed = line[index];
                line[index] = buffer[i] + delayed * feedback;
                index = index + 1 >= length ? 0 : index + 1;
                buffer[i] += delayed * mix;
            }
            return buffer;
        }

        public static float[] Tremolo(float[] buffer, float hz, float depth)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var lfo = (Mathf.Sin(TwoPi * hz * i / SampleRate) + 1f) * .5f;
                buffer[i] *= 1f - depth + depth * lfo;
            }
            return buffer;
        }

        // ------------------------------------------------------------------
        // 섞기 · 마무리
        // ------------------------------------------------------------------

        public static void Mix(float[] target, float[] source, float gain = 1f, float offsetSeconds = 0f)
        {
            var offset = Mathf.Max(0, Mathf.RoundToInt(offsetSeconds * SampleRate));
            var count = Mathf.Min(source.Length, target.Length - offset);
            for (var i = 0; i < count; i++) target[offset + i] += source[i] * gain;
        }

        public static float[] Normalize(float[] buffer, float peak = .88f)
        {
            var maximum = 0f;
            foreach (var sample in buffer) maximum = Mathf.Max(maximum, Mathf.Abs(sample));
            if (maximum <= 1e-5f) return buffer;

            var gain = peak / maximum;
            for (var i = 0; i < buffer.Length; i++) buffer[i] *= gain;
            return buffer;
        }

        /// <summary>
        /// 길이를 잘라 냅니다. 효과음이 길면 다음 소리와 겹쳐 답답해지므로,
        /// 합성한 클립은 모두 이 상한을 넘지 않게 맞춥니다.
        /// </summary>
        public static float[] Limit(float[] buffer, float maxSeconds)
        {
            var maximum = Mathf.Max(1, Mathf.CeilToInt(maxSeconds * SampleRate));
            if (buffer.Length <= maximum) return buffer;

            var trimmed = new float[maximum];
            Array.Copy(buffer, trimmed, maximum);
            // 자른 자리에서 뚝 끊기지 않도록 끝을 부드럽게 내립니다.
            return FadeOut(trimmed, Mathf.Min(.06f, maxSeconds * .25f));
        }

        public static AudioClip ToClip(string clipName, float[] buffer)
        {
            // 끝을 아주 살짝 깎아 재생이 끊길 때 나는 딸깍임을 없앱니다.
            FadeOut(buffer, .006f);
            var clip = AudioClip.Create(clipName, buffer.Length, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }
    }
}
