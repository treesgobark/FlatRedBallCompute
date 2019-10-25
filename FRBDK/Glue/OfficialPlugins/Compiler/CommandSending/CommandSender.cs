﻿using FlatRedBall.Glue.Plugins.ExportedImplementations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace OfficialPlugins.Compiler.CommandSending
{

    public static class CommandSender
    {
        public static async void SendCommand(string text)
        {

            TcpClient client = new TcpClient();
            client.Connect("localhost", 8021);

            // Stream string to server
            Stream stm = client.GetStream();
            //ASCIIEncoding asen = new ASCIIEncoding();
            //byte[] ba = asen.GetBytes(input);
            byte[] messageAsBytes = System.Text.ASCIIEncoding.UTF8.GetBytes(text);
            stm.Write(messageAsBytes, 0, messageAsBytes.Length);

            // give the server time to finish what it's doing:
            await Task.Delay(100000);

            //// Read response from server.
            byte[] buffer = new byte[1024];
            int bytesRead = stm.Read(buffer, 0, buffer.Length);
            var response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.WriteLine("Response String: " + response);

            client.Close();

            //Console.ReadLine();


        }
    }
}
