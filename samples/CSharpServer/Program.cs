//----------------------------------------------------------------------------
//
// Copyright (c) 2011-2012 Dave Thomas (@7sharp9) 
//                         Ryan Riley (@panesofglass)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Fracture.Http;

namespace CSharpServer
{
    class Program
    {
        static void Main(string[] args)
        {
            System.AppDomain.CurrentDomain.UnhandledException += Debug;

            var server = new HttpServer(env =>
            {
                var context = new Microsoft.Owin.OwinContext(env);
                var response = context.Response;
                response.StatusCode = 200;
                response.Headers.Add("Content-Type", new[] { "text/plain" });
                response.Headers.Add("Content-Length", new[] { "13" });
                response.Headers.Add("Server", new[] { "Fracture" });
                response.Write("Hello, world!");

                // Complete the Task.
                return Task.FromResult<object>(null);
            });

            server.Start(6667);
            Console.WriteLine("Running server on port 6667.");
            Console.ReadLine();
            server.Dispose();
        }

        static void Debug(object sender, UnhandledExceptionEventArgs x)
        {
            Console.WriteLine("{0}", x.ExceptionObject);
            Console.ReadLine();
        }
    }
}
