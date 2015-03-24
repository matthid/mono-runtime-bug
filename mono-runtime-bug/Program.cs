using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mono_runtime_bug
{
    class Program
    {
        static void Main(string[] args)
        {
            var started = new ManualResetEvent(false);
            var t = new Thread(() =>
            {
                try
                {
                    started.Set();
                    Thread.Sleep(Timeout.Infinite);
                }
                catch (ThreadAbortException)
                {
                    Thread.ResetAbort();
                }

                for (int i = 0; i < 20; i++)
                {
                    Console.WriteLine("Test {0}", i);
                }
            });
            t.IsBackground = false;
            t.Start();
            started.WaitOne(Timeout.Infinite);
            t.Abort();
        }
    }
}
