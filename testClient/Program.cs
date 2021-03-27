using System;
using Terminal;
using System.Threading;

namespace testClient
{
    class Program
    {
        static void Main(string[] args)
        {
            string str;
            BashTerminal bash = new BashTerminal();

            bash.WriteLine("echo \"hello World\"");

            bash.WriteLine("echo $(whoami) >> test.txt");
            bash.SudoUserNameAndPassword("username","passsword");
            bash.SudoRightToBash("/tmp/mysudooutput.txt");
            bash.WriteLine("echo $(whoami) >> test.txt");            
            bash.WriteLine("echo \"hello World2\"");
            Thread.Sleep(2000);
            str = bash.getWaitOnOutputData();

            Console.WriteLine(str);
        }
    }
}
