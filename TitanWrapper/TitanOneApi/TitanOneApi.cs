﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/*
Identifier List

0   PS4_PS        PS3_PS        XB1_XBOX   XB360_XBOX   WII_HOME   WII_HOME
1   PS4_SHARE     PS3_SELECT    XB1_VIEW   XB360_BACK   WII_MINUS  WII_MINUS
2   PS4_OPTIONS   PS3_START     XB1_MENU   XB360_START  WII_PLUS   WII_PLUS
3   PS4_R1        PS3_R1        XB1_RB     XB360_RB                WII_RT
4   PS4_R2        PS3_R2        XB1_RT     XB360_RT                WII_ZR
5   PS4_R3        PS3_R3        XB1_RS     XB360_RS     WII_ONE
6   PS4_L1        PS3_L1        XB1_LB     XB360_LB     WII_C      WII_LT
7   PS4_L2        PS3_L2        XB1_LT     XB360_LT     WII_Z      WII_ZL
8   PS4_L3        PS3_L3        XB1_LS     XB360_LS     WII_TWO
9   PS4_RX        PS3_RX        XB1_RX     XB360_RX                WII_RX
10  PS4_RY        PS3_RY        XB1_RY     XB360_RY                WII_RY
11  PS4_LX        PS3_LX        XB1_LX     XB360_LX     WII_NX     WII_LX
12  PS4_LY        PS3_LY        XB1_LY     XB360_LY     WII_NY     WII_LY
13  PS4_UP        PS3_UP        XB1_UP     XB360_UP     WII_UP     WII_UP
14  PS4_DOWN      PS3_DOWN      XB1_DOWN   XB360_DOWN   WII_DOWN   WII_DOWN
15  PS4_LEFT      PS3_LEFT      XB1_LEFT   XB360_LEFT   WII_LEFT   WII_LEFT
16  PS4_RIGHT     PS3_RIGHT     XB1_RIGHT  XB360_RIGHT  WII_RIGHT  WII_RIGHT
17  PS4_TRIANGLE  PS3_TRIANGLE  XB1_Y      XB360_Y                 WII_X
18  PS4_CIRCLE    PS3_CIRCLE    XB1_B      XB360_B      WII_B      WII_B
19  PS4_CROSS     PS3_CROSS     XB1_A      XB360_A      WII_A      WII_A
20  PS4_SQUARE    PS3_SQUARE    XB1_X      XB360_X                 WII_Y
21  PS4_ACCX      PS3_ACCX                              WII_ACCX
22  PS4_ACCY      PS3_ACCY                              WII_ACCY
23  PS4_ACCZ      PS3_ACCZ                              WII_ACCZ
24  PS4_GYROX     PS3_GYRO
25  PS4_GYROY                                           WII_ACCNX
26  PS4_GYROZ                                           WII_ACCNY
27  PS4_TOUCH                                           WII_ACCNZ
28  PS4_TOUCHX                                          WII_IRX
29  PS4_TOUCHY                                          WII_IRY

*/
namespace TitanWrapper.TitanOneApi
{
    public class TitanOne
    {
        public enum InputType
        {
            None,
            PS3 = 0x10,
            XB360 = 0x20,
            WII = 0x30,
            PS4 = 0x40,
            XB1 = 0x50
        };

        public enum OutputType
        {
            None, PS3, XB360, PS4, XB1
        }

        private IntPtr hModule;
        private bool functionsLoaded;
        dynamic callback;
        private Thread titanWatcher;
        bool threadRunning = false;

        private InputType inputType = InputType.None;
        public InputType CurrentInputType { get { return inputType ; } }

        private OutputType outputType = OutputType.None;
        public OutputType CurrentOutputType { get { return outputType; } }

        private GCMAPIStatus[] inputState = new GCMAPIStatus[30];
        private sbyte[] outputState = new sbyte[GCMAPIConstants.Output];

        public static readonly Dictionary<OutputType, InputType> outputToInputType = new Dictionary<OutputType, InputType>()
        {
            {OutputType.None, InputType.None },
            {OutputType.PS3, InputType.PS3 },
            {OutputType.PS4, InputType.PS4 },
            {OutputType.XB360, InputType.XB360 },
            {OutputType.XB1, InputType.XB1 },
        };

        public static readonly Dictionary<InputType, OutputType> inputToOutputType = new Dictionary<InputType, OutputType>()
        {
            {InputType.None, OutputType.None },
            {InputType.PS3, OutputType.PS3 },
            {InputType.PS4, OutputType.PS4 },
            {InputType.XB360, OutputType.XB360 },
            {InputType.XB1, OutputType.XB1 },
            {InputType.WII, OutputType.None },
        };

        public GCDAPI_Load Load;
        public GCDAPI_Unload Unload;
        public GCAPI_IsConnected IsConnected;
        public GCAPI_GetFWVer GetFWVer;
        public GCAPI_Read Read;
        public GCAPI_Write Write;
        public GCAPI_GetTimeVal GetTimeVal;
        public GCAPI_CalcPressTime CalcPressTime;

        public TitanOne(dynamic cb)
        {
            callback = cb;
            titanWatcher = new Thread(TitanWatcher);
        }

        // Destructor, fires on exit
        ~TitanOne()
        {
            if (functionsLoaded)
            {
                Unload();
                Console.WriteLine("Unloaded API");
            }
            UnloadDll();
            functionsLoaded = false;
            Console.WriteLine("Unloaded DLL");
        }


        public bool Init()
        {
            if (!functionsLoaded)
            {
                try
                {
                    String Working = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    hModule = LoadLibrary(Path.Combine(Working, "gcdapi.dll"));

                    if (hModule == IntPtr.Zero)
                    {
                        return false;
                    }
                    Console.WriteLine("Loaded DLL");

                    Load = GetFunction<GCDAPI_Load>(hModule, "gcdapi_Load");
                    Console.WriteLine((Load == null ? "Failed to obtain function '" : "Obtained function '") + "GCDAPI_Load" + "'");

                    Unload = GetFunction<GCDAPI_Unload>(hModule, "gcdapi_Unload");
                    Console.WriteLine((Unload == null ? "Failed to obtain function '" : "Obtained function '") + "GCDAPI_Unload" + "'");

                    IsConnected = GetFunction<GCAPI_IsConnected>(hModule, "gcapi_IsConnected");
                    Console.WriteLine((IsConnected == null ? "Failed to obtain function '" : "Obtained function '") + "GCAPI_IsConnected" + "'");

                    GetFWVer = GetFunction<GCAPI_GetFWVer>(hModule, "gcapi_GetFWVer");
                    Console.WriteLine((GetFWVer == null ? "Failed to obtain function '" : "Obtained function '") + "GCAPI_GetFWVer" + "'");

                    Read = GetFunction<GCAPI_Read>(hModule, "gcapi_Read");
                    Console.WriteLine((Read == null ? "Failed to obtain function '" : "Obtained function '") + "GCAPI_Read" + "'");

                    Write = GetFunction<GCAPI_Write>(hModule, "gcapi_Write");
                    Console.WriteLine((Write == null ? "Failed to obtain function '" : "Obtained function '") + "GCAPI_Write" + "'");

                    GetTimeVal = GetFunction<GCAPI_GetTimeVal>(hModule, "gcapi_GetTimeVal");
                    Console.WriteLine((GetTimeVal == null ? "Failed to obtain function '" : "Obtained function '") + "GCAPI_GetTimeVal" + "'");

                    CalcPressTime = GetFunction<GCAPI_CalcPressTime>(hModule, "gcapi_CalcPressTime");
                    Console.WriteLine((CalcPressTime == null ? "Failed to obtain function '" : "Obtained function '") + "GCAPI_CalcPressTime" + "'");

                    functionsLoaded = Load();
                    
                }
                catch
                {
                    functionsLoaded = false;
                    threadRunning = false;
                }
            }

            if (functionsLoaded && !threadRunning)
            {
                threadRunning = true;
                titanWatcher.Start();
            }

            RefreshControllerTypes();

            return functionsLoaded;
        }

        public void RefreshControllerTypes()
        {
            Stopwatch watch = new Stopwatch();
            while ((outputType == TitanOne.OutputType.None || inputType == TitanOne.InputType.None) && watch.ElapsedMilliseconds < 3000)
            {
                var report = GetReport();
                inputType = GetInputType();
                outputType = GetOutputType();
                Thread.Sleep(10);
            }
        }

        public bool SetOutputIdentifier(int identifier, int state)
        {
            outputState[identifier] = (sbyte)state;
            Write(outputState);
            return true;
        }

        public GCMAPIReport GetReport()
        {
            GCMAPIReport report = new GCMAPIReport();
            Read(ref report);
            return report;
        }

        public InputType GetInputType()
        {
            var report = GetReport();
            return (InputType)report.Controller;
        }

        public OutputType GetOutputType()
        {
            var report = GetReport();
            return (OutputType)report.Console;
        }

        public void UnloadDll()
        {
            FreeLibrary(hModule);
        }

        private void TitanWatcher()
        {
            GCMAPIReport report = new GCMAPIReport();

            while (threadRunning)
            {
                try
                {
                    if (!Read(ref report))
                    {
                        if (!IsConnected())
                        {
                            //break;
                            throw new Exception();
                        }
                    }

                    for (byte identifier = 0; identifier < GCMAPIConstants.Input; identifier++)
                    {
                        sbyte value = report.Input[identifier].Value;

                        if (value != inputState[identifier].Value)
                        {
                            IdentifierChanged(identifier, value);
                        }
                        //Console.WriteLine(String.Format("Index: {0}, Value: {1}", identifier, value));
                    }
                }
                catch
                {
                    //break;
                }
                finally
                {
                    Thread.Sleep(1);
                }
            }
        }

        private void IdentifierChanged(int identifier, int value)
        {
            inputState[identifier].Value = (sbyte)value;
            callback(identifier, value);

            //Console.WriteLine(String.Format("Identifier {0} changed to: {1}", identifier, value));
        }

        private static T GetFunction<T>(IntPtr hModule, String procName)
        {
            try
            {
                return (T)(object)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, procName), typeof(T));
            }
            catch
            {
                return default(T);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)]string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate bool GCDAPI_Load();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void GCDAPI_Unload();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate bool GCAPI_IsConnected();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate ushort GCAPI_GetFWVer();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate ushort GPPAPI_DevicePID();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate bool GCAPI_Read([In, Out] ref GCMAPIReport Report);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate bool GCAPI_Write(sbyte[] Output);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint GCAPI_GetTimeVal();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint GCAPI_CalcPressTime(uint Button);

#pragma warning disable 0649

        public struct GCMAPIConstants
        {
            public const int Input = 30;
            public const int Output = 36;
        }

        public struct GCMAPIStatus
        {
            public sbyte Value;
            public sbyte Previous;
            public int Holding;
        }

        public struct GCMAPIReport
        {
            public byte Console;
            public byte Controller;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] LED;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] Rumble;
            public byte Battery;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = GCMAPIConstants.Input, ArraySubType = UnmanagedType.Struct)]
            public GCMAPIStatus[] Input;
        }

#pragma warning restore 0649

    }
}