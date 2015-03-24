using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mono_runtime_bug
{
    class Program
    {
        static AppDomain CreateDomain(string name)
        {
            AppDomainSetup adSetup = new AppDomainSetup();
            adSetup.ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            var current = AppDomain.CurrentDomain;
            // You only need to add strongnames when your appdomain is not a full trust environment.
            var strongNames = new StrongName[0];

            return AppDomain.CreateDomain(
                name, null,
                current.SetupInformation, new PermissionSet(PermissionState.Unrestricted),
                strongNames);
        }

        /// <summary>
        /// Check if the given AppDomain is unloaded.
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static bool IsUnloaded(AppDomain domain)
        {
            Action<string> ignore = z => { };
            try
            {
                ignore(domain.FriendlyName);
                Console.WriteLine("AppDomain alive");
                return false;
            }
            catch (System.Threading.ThreadAbortException)
            { // Mono bug, it throws ThreadAbortExceptions a lot more aggressively
                // We will shutdown after cleanup, se leave us alone.
                Console.WriteLine("ThreadAbortException");
                Thread.ResetAbort();
                return true;
            }
            catch (System.Runtime.Remoting.RemotingException)
            { // Mono bug, should throw AppDomainUnloadedException instead
                Console.WriteLine("RemotingException");
                return true;
            }
            catch (AppDomainUnloadedException)
            {
                Console.WriteLine("AppDomainUnloadedException");
                return true;
            }
        }

        public class CleanupHelper : MarshalByRefObject
        {
            internal void Init(AppDomain appDomain)
            {
                appDomain.DomainUnload += appDomain_DomainUnload;
            }

            void appDomain_DomainUnload(object sender, EventArgs e)
            {
                Console.WriteLine("Notice AppDomain.DomainUnload...");
                var started = new ManualResetEvent(false);
                AppDomain other = (AppDomain)sender;
                var t = new Thread(() =>
                {
                    try
                    {
                        started.Set();
                        Console.WriteLine("Waiting for Shutdown");
                        while (!IsUnloaded(other))
                        {
                            Thread.Sleep(100);
                        }
                        Console.WriteLine("AppDomain unloaded");
                        Thread.Sleep(1000);
                        Console.WriteLine("Work finished.");
                    }
                    catch (Exception exn)
                    {
                        Console.WriteLine("FAILED: {0}", exn);
                    }

                    for (int i = 1; i <= 10; i++)
                    {
                        Thread.Sleep(100);
                        Console.WriteLine("Test {0}", i);
                    }
                });
                t.IsBackground = false;
                t.Start();
                started.WaitOne(Timeout.Infinite);
            }
        }

        static void Main(string[] args)
        {
            if (AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                // RazorEngine cannot clean up from the default appdomain...
                Console.WriteLine("Switching to second AppDomain...");
                var domain = CreateDomain("MyMainDomain");
                var exitCode = domain.ExecuteAssembly(Assembly.GetExecutingAssembly().Location, new[] { "init" });

                Console.WriteLine("unload second AppDomain");
                AppDomain.Unload(domain);
                return;
            }
            Console.WriteLine("Setting up cleanup domain");
            var cleanupDomain = CreateDomain("CleanupDomain");
            var handle = cleanupDomain.CreateInstanceFrom(Assembly.GetExecutingAssembly().Location, typeof(CleanupHelper).FullName);
            var helper = (CleanupHelper)handle.Unwrap();
            helper.Init(AppDomain.CurrentDomain);

            Console.WriteLine("Waiting for exit.");
            Thread.Sleep(1000);
            Console.WriteLine("Finish work of second AppDomain");
        }
    }
}
