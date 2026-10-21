using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PMStudio
{
    public sealed class ApiServer : IDisposable
    {
        readonly int port;
        readonly TcpListener listener;
        readonly SemaphoreSlim slots=new SemaphoreSlim(2,2);
        volatile bool running;
        public ApiServer(int portNumber)
        {
            if(portNumber<1024||portNumber>65535)throw new ArgumentException("API 포트: 1024~65535");
            port=portNumber;listener=new TcpListener(IPAddress.Loopback,port);
        }
        public void Start() { listener.Start(8);running=true;Thread thread=new Thread(Accept);thread.IsBackground=true;thread.Start(); }
        void Accept()
        {
            while(running)
            {
                TcpClient c;
                try{c=listener.AcceptTcpClient();}catch{if(!running)return;continue;}
                if(!slots.Wait(0)){c.Close();continue;}
                ThreadPool.QueueUserWorkItem(o=> { try{Serve(c);}finally{c.Close();slots.Release();} });
            }
        }
        static void Send(Stream s,int status,object data)
        {
            byte[] b=Encoding.UTF8.GetBytes(Files.Json(data));
            string reason=status==200?"OK":status==400?"Bad Request":status==403?"Forbidden":status==404?"Not Found":status==413?"Payload Too Large":status==415?"Unsupported Media Type":status==422?"Unprocessable Entity":"Internal Server Error";
            byte[] h=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+" "+reason+"\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: "+b.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n\r\n");
            s.Write(h,0,h.Length);s.Write(b,0,b.Length);
        }
        void Serve(TcpClient c)
        {
            c.ReceiveTimeout=10000;c.SendTimeout=10000;
            using(NetworkStream s=c.GetStream())
            {
                try
                {
                    List<byte> head=new List<byte>();int state=0;
                    while(state<4)
                    {
                        int ch=s.ReadByte();if(ch<0)return;head.Add((byte)ch);if(head.Count>16384){Send(s,400,new{error="Headers exceed 16 KB"});return;}
                        if((state==0||state==2)&&ch==13)state++;else if((state==1||state==3)&&ch==10)state++;else state=ch==13?1:0;
                    }
                    string[] lines=Encoding.ASCII.GetString(head.ToArray()).Split(new[]{"\r\n"},StringSplitOptions.None);string[] req=lines[0].Split(' ');
                    if(req.Length!=3||req[2]!="HTTP/1.1"){Send(s,400,new{error="HTTP/1.1 required"});return;}
                    Dictionary<string,string> headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                    for(int i=1;i<lines.Length;i++)if(lines[i].Length>0){int n=lines[i].IndexOf(':');if(n<1||headers.ContainsKey(lines[i].Substring(0,n))){Send(s,400,new{error="Invalid or duplicate header"});return;}headers.Add(lines[i].Substring(0,n),lines[i].Substring(n+1).Trim());}
                    string host;headers.TryGetValue("Host",out host);
                    if(host!="127.0.0.1:"+port&&host!="localhost:"+port){Send(s,403,new{error="Loopback Host required"});return;}
                    if(headers.ContainsKey("Origin")){Send(s,403,new{error="Browser cross-origin requests are not supported. Use a native local client."});return;}
                    if(headers.ContainsKey("Transfer-Encoding")){Send(s,400,new{error="Chunked bodies are not supported"});return;}
                    if(req[0]=="GET"&&req[1]=="/v1/health") { Send(s,200,new{status="ready",engine="PMStudio.Core/0.4.0",scope="nominal RC section",units="mm-MPa-kN-kNm"});return; }
                    if(req[0]=="GET"&&req[1]=="/v1/examples/rectangle") { Send(s,200,Examples.Rectangle());return; }
                    if(req[0]=="GET"&&req[1]=="/v1/schema") { Send(s,200,new{projectFormat="PMStudio.Project",version="1.0",model="RC-RectangularBlock-Nominal-v1",units="mm-MPa-kN-kNm",example="GET /v1/examples/rectangle",analysis="POST /v1/analyze with complete project JSON",limits="2 MB body; 40 loads; 300 bars; 800 vertices; 2 concurrent requests",strength="Nominal. No code factors or member stability checks."});return; }
                    if(req[0]!="POST"||req[1]!="/v1/analyze"){Send(s,404,new{error="Unknown endpoint"});return;}
                    string type;if(!headers.TryGetValue("Content-Type",out type)||!type.StartsWith("application/json",StringComparison.OrdinalIgnoreCase)){Send(s,415,new{error="Content-Type: application/json required"});return;}
                    string len;int count;if(!headers.TryGetValue("Content-Length",out len)||!int.TryParse(len,out count)||count<1){Send(s,400,new{error="Positive Content-Length required"});return;}
                    if(count>2000000){Send(s,413,new{error="Request exceeds 2 MB"});return;}
                    byte[] data=new byte[count];int read=0;while(read<count){int n=s.Read(data,read,count-read);if(n==0)return;read+=n;}
                    Project p=Files.Parse(new UTF8Encoding(false,true).GetString(data));Analysis r=Engine.Analyze(p);Send(s,200,r);
                }
                catch(ArgumentException ex){SafeError(s,400,ex.Message);}
                catch(ArithmeticException ex){SafeError(s,422,ex.Message);}
                catch(InvalidOperationException ex){SafeError(s,400,ex.Message);}
                catch(IOException){ }
                catch(Exception){SafeError(s,500,"Internal calculation error");}
            }
        }
        static void SafeError(Stream s,int code,string message) { try{Send(s,code,new{error=message});}catch{ } }
        public void Dispose() { running=false;listener.Stop(); }
    }
}
