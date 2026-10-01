using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Denba.Chzzk.Utility
{
    public static class WindowsFileDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileNameW([In, Out] ref OpenFileName ofn);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetCurrentDirectory(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetCurrentDirectory(uint nLen, StringBuilder buf);

        private const int OFN_ALLOWMULTISELECT = 0x00000200;
        private const int OFN_EXPLORER = 0x00080000;
        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int BufferSize = 260 * 100;

        public static async UniTask<List<string>> OpenMultipleAsync(
            string title = "이미지 파일 선택",
            string initialDir = "",
            string filter = "Image Files\0*.png;*.jpg;*.jpeg\0All Files\0*.*\0")
        {
            if (string.IsNullOrEmpty(initialDir))
                initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            // 다이얼로그가 작업 디렉터리를 바꾸므로 끝난 뒤 되돌린다.
            var dirBuf = new StringBuilder(260);
            GetCurrentDirectory(260, dirBuf);
            string savedDir = dirBuf.ToString();

            List<string> result = null;
            await UniTask.RunOnThreadPool(() => result = OpenDialog(title, initialDir, filter));

            SetCurrentDirectory(savedDir);

            return result ?? new List<string>();
        }

        public static List<string> OpenMultiple(
            string title = "이미지 파일 선택",
            string initialDir = "",
            string filter = "Image Files\0*.png;*.jpg;*.jpeg\0All Files\0*.*\0")
            => OpenMultipleAsync(title, initialDir, filter).GetAwaiter().GetResult();

        // 공용 파일 대화상자는 STA 스레드에서만 안정적으로 동작한다.
        private static List<string> OpenDialog(string title, string initialDir, string filter)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            List<string> staResult = null;
            var thread = new Thread(() => staResult = RunDialog(title, initialDir, filter));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return staResult ?? new List<string>();
#else
            return new List<string>();
#endif
        }

        private static List<string> RunDialog(string title, string initialDir, string filter)
        {
            var result = new List<string>();
            IntPtr bufPtr = Marshal.AllocHGlobal(BufferSize * sizeof(char));

            try
            {
                Marshal.Copy(new byte[BufferSize * sizeof(char)], 0, bufPtr, BufferSize * sizeof(char));

                var ofn = new OpenFileName
                {
                    lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
                    lpstrFilter = filter,
                    lpstrFile = bufPtr,
                    nMaxFile = BufferSize,
                    lpstrTitle = title,
                    lpstrInitialDir = initialDir,
                    Flags = OFN_ALLOWMULTISELECT | OFN_EXPLORER | OFN_FILEMUSTEXIST,
                };

                if (!GetOpenFileNameW(ref ofn)) return result;

                var strings = ParseMultiString(bufPtr, BufferSize);
                if (strings.Count == 0) return result;

                if (strings.Count == 1)
                    result.Add(strings[0]);
                else
                {
                    string dir = strings[0];
                    for (int i = 1; i < strings.Count; i++)
                        result.Add(Path.Combine(dir, strings[i]));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(bufPtr);
            }

            return result;
        }

        private static List<string> ParseMultiString(IntPtr ptr, int maxChars)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            int offset = 0;

            while (offset < maxChars)
            {
                char ch = (char)Marshal.ReadInt16(ptr, offset * 2);
                offset++;

                if (ch == '\0')
                {
                    if (current.Length == 0) break;
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }

            return result;
        }
    }
}