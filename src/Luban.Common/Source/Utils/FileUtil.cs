using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Luban.Common.Utils
{
    public static class FileUtil
    {
        private static readonly NLog.Logger s_logger = NLog.LogManager.GetCurrentClassLogger();

        public static string GetApplicationDirectory()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetCallingAssembly().Location);
        }

        public static string GetPathRelateApplicationDirectory(string relatePath)
        {
            return Path.Combine(GetApplicationDirectory(), relatePath);
        }

        public static string GetFileName(string path)
        {
            int index = path.Replace('\\', '/').LastIndexOf('/');
#if !LUBAN_LITE
            return index >= 0 ? path[(index + 1)..] : path;
#else
            return index >= 0 ? path.Substring(index + 1, path.Length - index - 1) : path;
#endif
        }

        public static string GetParent(string path)
        {
            int index = path.Replace('\\', '/').LastIndexOf('/');
#if !LUBAN_LITE
            return index >= 0 ? path[..index] : ".";
#else
            return index >= 0 ? path.Substring(0, index) : ".";
#endif
        }

        public static string GetFileNameWithoutExt(string file)
        {
            int index = file.LastIndexOf('.');
            return index >= 0 ? file.Substring(0, index) : file;
        }

        public static string GetFileExtension(string file)
        {
            int index = file.LastIndexOf('.');
            return index >= 0 ? file.Substring(index + 1) : "";
        }

        public static string GetPathRelateRootFile(string rootFile, string file)
        {
            return Combine(GetParent(rootFile), file);
        }

        /// <summary>
        ///  忽略以 文件名以 '.' '_' '~' 开头的文件
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static bool IsValidInputFile(string file)
        {
            if (!File.Exists(file))
            {
                return false;
            }
            var f = new FileInfo(file);
            string fname = f.Name;
#if !LUBAN_LITE
            return !fname.StartsWith('.') && !fname.StartsWith('_') && !fname.StartsWith('~');
#else
            return !fname.StartsWith(".") && !fname.StartsWith("_") && !fname.StartsWith("~");
#endif
        }

        [ThreadStatic]
        private static MD5 s_cacheMd5;

        private static MD5 CacheMd5
        {
            get
            {
                var md5 = s_cacheMd5 ??= MD5.Create();
                md5.Clear();
                return md5;
            }
        }

        public static string CalcMD5(byte[] srcBytes)
        {
            using MD5 md5 = MD5.Create();
            var md5Bytes = md5.ComputeHash(srcBytes);
            var s = new StringBuilder(md5Bytes.Length * 2);
            foreach (var b in md5Bytes)
            {
                s.Append(b.ToString("X"));
            }
            return s.ToString();
        }

        public static string Standardize(string path)
        {
            return path.Replace('\\', '/');
        }

        public static string Combine(string parent, string sub)
        {
            return Standardize(Path.Combine(parent, sub));
        }

        public static bool IsExcelFile(string fullName)
        {
            return fullName.EndsWith(".csv", StringComparison.Ordinal)
                || fullName.EndsWith(".xls", StringComparison.Ordinal)
                || fullName.EndsWith(".xlsx", StringComparison.Ordinal);
        }

        public static (string, string) SplitFileAndSheetName(string url)
        {
            int sheetSepIndex = url.IndexOf('@');
            if (sheetSepIndex < 0)
            {
                return (url, null);
            }
            else
            {
                int lastPathSep = url.LastIndexOf('/', sheetSepIndex);
#if !LUBAN_LITE
                if (lastPathSep >= 0)
                {
                    return (url[0..(lastPathSep + 1)] + url[(sheetSepIndex + 1)..], url[(lastPathSep + 1)..sheetSepIndex]);
                }
                else
                {
                    return (url[(sheetSepIndex + 1)..], url[(lastPathSep + 1)..sheetSepIndex]);
                }
#else
                if (lastPathSep >= 0)
                {
                    return (url.Substring(0, lastPathSep + 1) + url.Substring(sheetSepIndex + 1), url.Substring(lastPathSep + 1, sheetSepIndex - lastPathSep - 1));
                }
                else
                {
                    return (url.Substring(sheetSepIndex + 1), url.Substring(lastPathSep + 1, sheetSepIndex - lastPathSep - 1));
                }
#endif
            }
        }

        public static async Task SaveFileAsync(string relateDir, string filePath, byte[] content)
        {
            // 调用此接口时，已保证 文件必然是改变的，不用再检查对比文件
            var outputPath = Standardize(relateDir != null ? System.IO.Path.Combine(relateDir, filePath) : filePath);
            Directory.GetParent(outputPath).Create();
            if (File.Exists(outputPath))
            {
                //if (CheckFileNotChange(outputPath, content))
                //{
                //    s_logger.Trace("[not change] {file}", outputPath);
                //    return;
                //}
                //else
                //{
                //    s_logger.Info("[override] {file}", outputPath);
                //    if (File.GetAttributes(outputPath).HasFlag(FileAttributes.ReadOnly))
                //    {
                //        File.SetAttributes(outputPath, FileAttributes.Normal);
                //    }
                //}
                s_logger.Info("[override] {file}", outputPath);
                if (File.GetAttributes(outputPath).HasFlag(FileAttributes.ReadOnly))
                {
                    File.SetAttributes(outputPath, FileAttributes.Normal);
                }
            }
            else
            {
                s_logger.Info("[new] {file}", outputPath);
            }


#if !LUBAN_LITE
            await File.WriteAllBytesAsync(outputPath, content);
#else
            await Task.Run(() => File.WriteAllBytes(outputPath, content));
#endif
        }

        public static async Task<byte[]> ReadAllBytesAsync(string file)
        {
            // File.ReadAllBytesAsync 无法读取被打开的excel文件，只好重新实现了一个
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long count = fs.Length;
            var bytes = new byte[count];
            int writeIndex = 0;
            while (writeIndex < count)
            {
                int n = await fs.ReadAsync(bytes, writeIndex, (int)count - writeIndex, default);
                writeIndex += n;
            }
            return bytes;
        }

        public static byte[] ReadAllBytes(string file)
        {
            // File.ReadAllBytesAsync 无法读取被打开的excel文件，只好重新实现了一个
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long count = fs.Length;
            var bytes = new byte[count];
            int writeIndex = 0;
            while (writeIndex < count)
            {
                int n = fs.Read(bytes, writeIndex, (int)count - writeIndex);
                writeIndex += n;
            }
            return bytes;
        }

        public static void DeleteDirectoryRecursive(string rootDir)
        {
            string[] files = Directory.GetFiles(rootDir);
            string[] dirs = Directory.GetDirectories(rootDir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectoryRecursive(dir);
            }

            Directory.Delete(rootDir, false);
        }
    }
}
