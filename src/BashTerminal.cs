using System;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace Terminal
{
    public class BashTerminal {

        public Process process;

        private Thread ThreadOutputDataReceived;
        private Thread ThreadErrorDataReceived;
        private string sudoUserName = "";
        private string sudoUserNamePassword = "";
        
        private StringBuilder strBuilder;
        
        SpinLock spinlock = new SpinLock();
        

        public BashTerminal() {   

            strBuilder = new StringBuilder();
            process = new Process();
            process.StartInfo.FileName = "bash";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;   
            
            
            process.Start();

            // note ErrorDataReceived and OutputDataReceived are not thread safe
            // and does not work in a class dll file in linux well
            // it created verids bugs. If you put all code to same dll file it works fine
            // MS docs say do not setup both ErrorDataReceived and OutputDataReceived 
            // it will create a dead lock in process as well.
            // Here is my own version of 
            ThreadOutputDataReceived = new Thread(new ThreadStart(WorkThreadBashTerminalOutPut));
            ThreadErrorDataReceived = new Thread(new ThreadStart(WorkThreadBashTerminalError));
            
            ThreadOutputDataReceived.Start();
            ThreadErrorDataReceived.Start();
            
            

        }

        /// <summary>
        /// Checking if any new standard output or error message have genreated and return them, and reset the internal
        /// text buffer for standard and error message.
        /// </summary>        
        /// <returns>Return current contain of standard and error msg</returns>
        public string getOutputData() {
            
            string Data;
            bool gotLock = false;

            try {                
                spinlock.Enter(ref gotLock);
                Data = strBuilder.ToString();
                strBuilder.Clear();
            } 
            finally {
                if (gotLock) spinlock.Exit();
            }
            
            return Data;          
        }

        /// <summary>
        /// Waiting on any new standard output or error message have genreated and return them, and reset the internal
        /// text buffer for standard and error message.
        /// </summary>        
        /// <returns>Return current contain of standard and error msg</returns>

        public string getWaitOnOutputData() {

            string Data;

            Data = "";

            while ( string.IsNullOrEmpty(Data) ) {
                
                Data = this.getOutputData();

                if ( string.IsNullOrEmpty(Data) ) {

                    Thread.Sleep(100); 

                }                
            }
            return Data;
        }
        
        private void WorkThreadBashTerminalOutPut() {   
            
            string Data;
            bool gotLock = false;

            // Note StandardOutput.ReadLine is waiting on next line is comming
            // basic we are in loop if no new data have arive
            Data = process.StandardOutput.ReadLine();
                   
            // Do not save empty line, lock cost time
            if (!String.IsNullOrEmpty( Data )) {   

                // Use spin lock, it is waiting until it can get a lock
                // be carefull with spinlock, MS Show example 
                // how to use spinLock with string building 
                // so it is safe using it with strBuilder
                // spinlock should not be use if you do not known how to use diffent lock and why
                // spinlock is one of the danguers lock you can do.
                try { 

                    spinlock.Enter(ref gotLock);
                    
                    strBuilder.AppendLine( Data );                                        

                } finally {
                    if (gotLock) spinlock.Exit();
                }
            }

            // Do not read the next line direcly let the thread sleep
            // other wise it create a verid bug some case no data 
            // can read from standard output
            if ( !String.IsNullOrEmpty( Data ) ) { 
                Thread.Sleep(10);
            } else {
                Thread.Sleep(100);    
            }                                   
        }

         private void WorkThreadBashTerminalError() {   
            
            string Data;
            bool gotLock = false;

            // Note StandardOutput.ReadLine is waiting on next line is comming
            // basic we are in loop if no new data have arive
            Data = process.StandardError.ReadLine();
                   
            // Do not save empty line, lock cost time
            if (!String.IsNullOrEmpty( Data )) {   

                // Use spin lock, it is waiting until it can get a lock
                // be carefull with spinlock, MS Show example 
                // how to use spinLock with string building 
                // so it is safe using it with strBuilder
                // spinlock should not be use if you do not known how to use diffent lock and why
                // spinlock is one of the danguers lock you can do.
                try { 

                    spinlock.Enter(ref gotLock);
                    
                    strBuilder.AppendLine( Data );                                        

                } finally {
                    if (gotLock) spinlock.Exit();
                }
            }

            // Do not read the next line direcly let the thread sleep
            // other wise it create a verid bug some case no data 
            // can read from standard output
            if ( !String.IsNullOrEmpty( Data ) ) { 
                Thread.Sleep(10);
            } else {
                Thread.Sleep(100);    
            }                                   
        }

        /// <summary>
        /// Change user for bash same as su username then prompt password
        /// This work with standard pipes as well. Not like sudo it break standard pipe completed
        /// </summary>                
        public void ChangeUser(string username, string password) {
            this.WriteLine("su " + username + Environment.NewLine + password);
        }

        /// <summary>
        /// Send you bash command 
        /// </summary>
        public void WriteLine(string inputLine) {           
            process.StandardInput.WriteLine( inputLine );  
            Thread.Sleep(10);
        } 

        /// <summary>
        /// Set sudo username and password
        /// </summary>
        public void SudoUserNameAndPassword( string username, string password) {
            this.sudoUserName = username;
            this.sudoUserNamePassword = password;
        }

        /// <summary>
        /// Grant root access to new bash shell inside current bash shell.
        /// We can not provide pass word direcly as we can do with su, sudo does not accpect it.
        /// Workaround the issue we reading the password from standard input then call sudo again 
        /// Bad with this workaround output is broken to main console so it logs to a file.
        /// That you deside the name of.
        /// with sudo bash        
        /// </summary>
        public void SudoRightToBash( string pathAndFileName ) {
            
            string inputLine;
            
            inputLine = "bash setsudo.sh";

            this.WriteLine("echo -e '#!/bin/sh\nhead -n 1' > $HOME/bashsudoreader.sh");
            this.WriteLine("chmod a+x $HOME/bashsudoreader.sh");             
            this.WriteLine("export SUDO_ASKPASS=\"$HOME/bashsudoreader.sh\"");
            this.WriteLine("sudo -A echo \"I'm root!\" -u " + this.sudoUserName  );
            this.WriteLine( this.sudoUserNamePassword );

            Console.WriteLine(inputLine);            
            this.WriteLine( inputLine );
            Thread.Sleep(100);
            this.WriteLine( "sudo bash 2>&1 > " + pathAndFileName );

        }

        /// <summary>
        /// Remove root access that have been granted from the bash, 
        /// if sudo already been granted one time before, next time some write sudo ... it 
        /// grant root access without password but if it new instands the root access is gone
        /// </summary>        
        public void SudoOrSuExitFromBash() {
            this.WriteLine( "exit" );            
        }

       
       

    }
}
