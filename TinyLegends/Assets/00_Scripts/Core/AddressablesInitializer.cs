using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace IdleBattle
{
    public readonly struct AddressablesDownloadProgress
    {
        public AddressablesDownloadProgress(float percent, long downloadedBytes, long totalBytes)
        {
            Percent = percent;
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
        }

        public float Percent { get; }
        public long DownloadedBytes { get; }
        public long TotalBytes { get; }
    }

    /// <summary>Refreshes remote catalogs and downloads all dependencies before game entry.</summary>
    public static class AddressablesInitializer
    {
        public static async Task<long> InitializeAndDownloadAsync(Action<AddressablesDownloadProgress> onProgress = null)
        {
            var initializeHandle = Addressables.InitializeAsync();
            await initializeHandle.Task;
            EnsureSucceeded(initializeHandle, "Addressables initialization");

            var catalogHandle = Addressables.CheckForCatalogUpdates(false);
            await catalogHandle.Task;
            EnsureSucceeded(catalogHandle, "Remote catalog check");
            var catalogs = catalogHandle.Result;
            if (catalogs != null && catalogs.Count > 0)
            {
                var updateHandle = Addressables.UpdateCatalogs(catalogs, false);
                await updateHandle.Task;
                EnsureSucceeded(updateHandle, "Remote catalog update");
                Addressables.Release(updateHandle);
            }
            Addressables.Release(catalogHandle);

            var keys = Addressables.ResourceLocators.SelectMany(locator => locator.Keys)
                .Where(key => key != null).Distinct().ToList();
            if (keys.Count == 0)
            {
                onProgress?.Invoke(new AddressablesDownloadProgress(1f, 0L, 0L));
                return 0L;
            }

            var sizeHandle = Addressables.GetDownloadSizeAsync(keys);
            await sizeHandle.Task;
            EnsureSucceeded(sizeHandle, "Addressables download-size check");
            var totalBytes = sizeHandle.Result;
            Addressables.Release(sizeHandle);
            if (totalBytes <= 0L)
            {
                onProgress?.Invoke(new AddressablesDownloadProgress(1f, 0L, 0L));
                return 0L;
            }

            onProgress?.Invoke(new AddressablesDownloadProgress(0f, 0L, totalBytes));
            var downloadHandle = Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union, false);
            while (!downloadHandle.IsDone)
            {
                var status = downloadHandle.GetDownloadStatus();
                onProgress?.Invoke(new AddressablesDownloadProgress(status.Percent, (long)status.DownloadedBytes, totalBytes));
                await Task.Yield();
            }

            EnsureSucceeded(downloadHandle, "Addressables dependency download");
            onProgress?.Invoke(new AddressablesDownloadProgress(1f, totalBytes, totalBytes));
            Addressables.Release(downloadHandle);
            return totalBytes;
        }

        private static void EnsureSucceeded<T>(AsyncOperationHandle<T> handle, string operation)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded) return;
            throw new InvalidOperationException($"{operation} failed.", handle.OperationException);
        }

        private static void EnsureSucceeded(AsyncOperationHandle handle, string operation)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded) return;
            throw new InvalidOperationException($"{operation} failed.", handle.OperationException);
        }
    }
}
