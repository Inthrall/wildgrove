// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("CLo5Ggg1PjESvnC+zzU5OTk9ODv1jsjk4xyHqcdYQBjPsOZPbxBrpJQP2De9sXQxt7N4Dab+fhuvNGHEBPa99Ta+BImLi3SspLoSzEJ0IpjveWNsYY3CmFpMeFSyt1tUfTljeLSVVdq5abwQDYu2tqEACn6XfiirJ3hh6rWFVUGsQQSvHhil3AcHnRWvzKD56ur7wN7fF/ze51/kNvVItWKIpvNB/uIVaAmyHjQ0sDxfnDPEpU78ubB06+vRL0FxxPbJtkjfPhrQi/o2YRnIjySfC2Lmh+uC+vlQ42ibv9/PhcVLQwwKHAsfkxpgAEUKujk3OAi6OTI6ujk5OPgEEB8mIwNLYhzT8vOcBOnGt+W4pYg6tQmHgTy9HkPwIqT17zo7OTg5");
        private static int[] order = new int[] { 0,3,6,10,6,9,7,11,8,11,11,13,13,13,14 };
        private static int key = 56;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
