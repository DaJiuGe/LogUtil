using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogUtil
{
    class Program
    {
        static void Main(string[] args)
        {
            LogUtil.Activate(LogUtil.LogMode.Async);
            LogUtil.LogBegin();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < 99999; i++)
            {
                LogUtil.Log($"{i}");
            }
            LogUtil.LogEnd();
            while (!LogUtil.IsLogFinished())
            {
                Thread.Sleep(10);
            }
            sw.Stop();
            Console.WriteLine($"Total : {sw.Elapsed.TotalMilliseconds} ms");
            LogUtil.Release();
            Console.ReadKey();
        }
    }
}
