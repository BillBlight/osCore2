﻿/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HttpServer;
using log4net;
using OpenMetaverse;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class PollServiceHttpRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public readonly PollServiceEventArgs PollServiceArgs;
        public readonly IHttpClientContext HttpContext;
        public readonly IHttpRequest Request;
        public readonly int RequestTime;
        public readonly UUID RequestID;
        public int  contextHash;

        private void GenContextHash()
        {
            Random rnd = new Random();
            contextHash = 0;
            if (Request.Headers["remote_addr"] != null)
                contextHash = (Request.Headers["remote_addr"]).GetHashCode() << 16;
            else
                contextHash = rnd.Next() << 16;
            if (Request.Headers["remote_port"] != null)
            {
                string[] strPorts = Request.Headers["remote_port"].Split(new char[] { ',' });
                contextHash += Int32.Parse(strPorts[0]);
            }
            else
                contextHash += rnd.Next() & 0xffff;
        }

        public PollServiceHttpRequest(
            PollServiceEventArgs pPollServiceArgs, IHttpClientContext pHttpContext, IHttpRequest pRequest)
        {
            PollServiceArgs = pPollServiceArgs;
            HttpContext = pHttpContext;
            Request = pRequest;
            RequestTime = System.Environment.TickCount;
            RequestID = UUID.Random();
            GenContextHash();
        }

        internal void DoHTTPGruntWork(BaseHttpServer server, Hashtable responsedata)
        {
            OSHttpResponse response
                = new OSHttpResponse(new HttpResponse(HttpContext, Request), HttpContext);

            byte[] buffer = server.DoHTTPGruntWork(responsedata, response);

            if(Request.Body.CanRead)
                Request.Body.Dispose();

            response.SendChunked = false;
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;
            response.ReuseContext = false;

            try
            {
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Flush();
                response.Send();
                buffer = null;
            }
            catch (Exception ex)
            {
                m_log.Warn("[POLL SERVICE WORKER THREAD]: Error ", ex);
            }

            PollServiceArgs.RequestsHandled++;
        }

        internal void DoHTTPstop(BaseHttpServer server)
        {
            OSHttpResponse response
                = new OSHttpResponse(new HttpResponse(HttpContext, Request), HttpContext);

            if(Request.Body.CanRead)
                Request.Body.Dispose();

            response.SendChunked = false;
            response.ContentLength64 = 0;
            response.ContentEncoding = Encoding.UTF8;
            response.ReuseContext = false;
            response.KeepAlive = false;
            response.SendChunked = false;
            response.StatusCode = 503;

            try
            {
                response.OutputStream.Flush();
                response.Send();
            }
            catch (Exception e)
            {
            }
        }
    }

    class PollServiceHttpRequestComparer : IEqualityComparer<PollServiceHttpRequest>
    {
        public bool Equals(PollServiceHttpRequest b1, PollServiceHttpRequest b2)
        {
            if (b1.contextHash != b2.contextHash)
                return false;
            bool b = Object.ReferenceEquals(b1.HttpContext, b2.HttpContext);
            return b;
        }

        public int GetHashCode(PollServiceHttpRequest b2)
        {
            return (int)b2.contextHash;
        }
    }
}