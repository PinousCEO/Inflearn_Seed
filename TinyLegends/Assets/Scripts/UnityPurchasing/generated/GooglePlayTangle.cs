// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("/xZbfGCv7KIFpUmLJUM7glQ/J1NByTFaOgTNm9rotn/q8+1BfD4xTTwSHRiul4bSmufqIUZZi+tKJ/92swGCobOOhYqpBcsFdI6CgoKGg4BY9AoCgn8AVQSzuhFotpjXjggn/ODgw4loOkOYoZTanHAVBjAuhs1zfodbp2bdNvdfLrUDvbbDuUyS8sYBgoyDswGCiYEBgoKDHRCR39Z+LLuxYlEd2Z//FVLoy5sZEeEXjSsFIEbV8WKhzOgDHdMiKD9CCwxJPeDls3FUh4FUXMNhSrFGZDZKOQK+oPSF56Yyb3hHyQxNTY3HICMV3xdem3FRT1Mi9YGL4fZak21ch0WVquPhGtQX1rCnHE2hno9BCnvbyDBdGIHh/8m0AIDdXIGAgoOC");
        private static int[] order = new int[] { 11,10,12,11,9,12,12,10,8,13,13,13,13,13,14 };
        private static int key = 131;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
