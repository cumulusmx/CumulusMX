using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace CumulusMX
{
    public class HttpProcessor
    {
        private const int BUF_SIZE = 4096;
        private static int MAX_POST_SIZE = 10*1024*1024; // 10MB
        public StreamWriter OutputStream;
        public Hashtable httpHeaders = new Hashtable();

        public String http_method;
        public String http_protocol_versionstring;
        public String http_url;
        private Stream inputStream;
        public TcpClient socket;
        public HttpServer srv;
        public String webroot = ".";


        public HttpProcessor(TcpClient s, HttpServer srv)
        {
            socket = s;
            this.srv = srv;
        }


        private string streamReadLine(Stream inputStream)
        {
            int next_char;
            string data = "";
            while (true)
            {
                next_char = inputStream.ReadByte();
                if (next_char == '\n')
                {
                    break;
                }
                if (next_char == '\r')
                {
                    continue;
                }
                if (next_char == -1)
                {
                    Thread.Sleep(1);
                    continue;
                }
                ;
                data += Convert.ToChar(next_char);
            }
            return data;
        }

        public void process()
        {
            // we can't use a StreamReader for input, because it buffers up extra data on us inside its
            // "processed" view of the world, and we want the data raw after the headers
            inputStream = new BufferedStream(socket.GetStream());

            // we probably shouldn't be using a streamwriter for all output from handlers either
            OutputStream = new StreamWriter(new BufferedStream(socket.GetStream()));
            try
            {
                parseRequest();
                readHeaders();
                if (http_method.Equals("GET"))
                {
                    handleGETRequest();
                }
                else if (http_method.Equals("POST"))
                {
                    handlePOSTRequest();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e);
                writeFailure();
            }
            OutputStream.Flush();
            // bs.Flush(); // flush any remaining output
            inputStream = null;
            OutputStream = null; // bs = null;            
            socket.Close();
        }

        public void parseRequest()
        {
            String request = streamReadLine(inputStream);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            http_method = tokens[0].ToUpper();
            http_url = tokens[1];
            http_protocol_versionstring = tokens[2];

            Console.WriteLine("starting: " + request);
        }

        public void readHeaders()
        {
            Console.WriteLine("readHeaders()");
            String line;
            while ((line = streamReadLine(inputStream)) != null)
            {
                if (line.Equals(""))
                {
                    Console.WriteLine("got headers");
                    return;
                }

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }
                String name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++; // strip any spaces
                }

                string value = line.Substring(pos, line.Length - pos);
                Console.WriteLine("header: {0}:{1}", name, value);
                httpHeaders[name] = value;
            }
        }

        public void handleGETRequest()
        {
            srv.HandleGetRequest(this);
        }

        public void handlePOSTRequest()
        {
            // this post data processing just reads everything into a memory stream.
            // this is fine for smallish things, but for large stuff we should really
            // hand an input stream to the request processor. However, the input stream 
            // we hand him needs to let him see the "end of the stream" at this content 
            // length, because otherwise he won't know when he's seen it all! 

            Console.WriteLine("get post data start");
            int content_len = 0;
            var ms = new MemoryStream();
            if (httpHeaders.ContainsKey("Content-Length"))
            {
                content_len = Convert.ToInt32(httpHeaders["Content-Length"]);
                if (content_len > MAX_POST_SIZE)
                {
                    throw new Exception(String.Format("POST Content-Length({0}) too big for this simple server", content_len));
                }
                var buf = new byte[BUF_SIZE];
                int to_read = content_len;
                while (to_read > 0)
                {
                    Console.WriteLine("starting Read, to_read={0}", to_read);

                    int numread = inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                    Console.WriteLine("read finished, numread={0}", numread);
                    if (numread == 0)
                    {
                        if (to_read == 0)
                        {
                            break;
                        }
                        else
                        {
                            throw new Exception("client disconnected during post");
                        }
                    }
                    to_read -= numread;
                    ms.Write(buf, 0, numread);
                }
                ms.Seek(0, SeekOrigin.Begin);
            }
            Console.WriteLine("get post data end");
            srv.HandlePostRequest(this, new StreamReader(ms));
        }

        public void writeSuccess(string contentType)
        {
            OutputStream.WriteLine("HTTP/1.0 200 OK");
            OutputStream.WriteLine("Content-Type: " + contentType);
            OutputStream.WriteLine("Connection: close");
            OutputStream.WriteLine("");
            OutputStream.Flush();
        }

        public void writeFailure()
        {
            OutputStream.WriteLine("HTTP/1.0 404 File not found");
            OutputStream.WriteLine("Connection: close");
            OutputStream.WriteLine("");
            OutputStream.Flush();
        }
    }

    public abstract class HttpServer
    {
        private bool is_active = true;
        private TcpListener listener;
        protected int port;

        public HttpServer(int port)
        {
            this.port = port;
        }

        public string LocalIPAddress()
        {
            IPHostEntry host;
            string localIP = "";
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                    break;
                }
            }
            return localIP;
        }

        public void listen()
        {
            IPAddress ipAddress = IPAddress.Parse(LocalIPAddress());
            listener = new TcpListener(ipAddress, port);
            listener.Start();
            Console.WriteLine("Cumulus running at http://" + listener.LocalEndpoint);
            while (is_active)
            {
                try
                {
                    TcpClient s = listener.AcceptTcpClient();
                    var processor = new HttpProcessor(s, this);
                    processor.webroot = "interface";
                    var thread = new Thread(processor.process);
                    thread.Start();
                    Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);
                }
            }
        }

        public void Stop()
        {
            is_active = false;
            listener.Stop();
        }

        public abstract void HandleGetRequest(HttpProcessor p);
        public abstract void HandlePostRequest(HttpProcessor p, StreamReader inputData);
    }

    public class MyHttpServer : HttpServer
    {
        private readonly Cumulus cumulus;

        public MyHttpServer(int port, Cumulus cumulus) : base(port)
        {
            this.cumulus = cumulus;
        }


        public override void HandleGetRequest(HttpProcessor p)
        {
            Console.WriteLine("request: {0}", p.http_url);

            if (p.http_url == "/data.json")
            {
                //var jss = new JavaScriptSerializer();

                Cumulus.CurrentData currentData = cumulus.GetCurrentData();

                var data = new DataStruct(cumulus,currentData.OutdoorTemperature, currentData.OutdoorHumidity, currentData.AvgTempToday, currentData.IndoorTemperature,
                    currentData.OutdoorDewpoint, currentData.WindChill, currentData.IndoorHumidity, currentData.Pressure, currentData.WindLatest, currentData.WindAverage,
                    currentData.Recentmaxgust, currentData.WindRunToday, currentData.Bearing, currentData.Avgbearing, currentData.RainToday, currentData.RainYesterday,
                    currentData.RainMonth, currentData.RainYear, currentData.RainRate, currentData.RainLastHour, currentData.HeatIndex, currentData.Humidex, currentData.AppTemp,
                    currentData.TempTrend, currentData.PressTrend, cumulus.WindUnitText);

                //var json = jss.Serialize(data);

                var ser = new DataContractJsonSerializer(typeof (DataStruct));

                p.writeSuccess(System.Web.MimeMapping.GetMimeMapping("data.json"));

                ser.WriteObject(p.OutputStream.BaseStream, data);
            }
            else
            {
                String doc = p.http_url == "/" ? "index.html" : p.http_url;
                String filename = p.webroot + doc;

                if (File.Exists(filename))
                {
                    FileStream inputStream = File.Open(filename, FileMode.Open);
                    var reader = new StreamReader(inputStream);

                    p.writeSuccess(System.Web.MimeMapping.GetMimeMapping(filename));

                    p.OutputStream.Write(reader.ReadToEnd());

                    inputStream.Close();
                }
                else
                {
                    p.writeFailure();
                }
            }
        }

        public override void HandlePostRequest(HttpProcessor p, StreamReader inputData)
        {
            Console.WriteLine("POST request: {0}", p.http_url);
            string data = inputData.ReadToEnd();

            p.OutputStream.WriteLine("<html><body><h1>test server</h1>");
            p.OutputStream.WriteLine("<a href=/test>return</a><p>");
            p.OutputStream.WriteLine("postbody: <pre>{0}</pre>", data);
        }
    }
}