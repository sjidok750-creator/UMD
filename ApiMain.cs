using System;
using System.Threading;
namespace PMStudio
{
    static class ApiMain
    {
        static int Main(string[] args)
        {
            try
            {
                int port=args.Length==0?18741:int.Parse(args[0]);using(ApiServer api=new ApiServer(port))using(ManualResetEvent done=new ManualResetEvent(false))
                {
                    Console.CancelKeyPress+=(s,e)=>{e.Cancel=true;done.Set();};api.Start();Console.WriteLine("PM Studio + local API: http://127.0.0.1:"+port+"/v1/health");Console.WriteLine("Nominal RC section engine. Press Ctrl+C to stop.");done.WaitOne();
                }return 0;
            }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
        }
    }
}
