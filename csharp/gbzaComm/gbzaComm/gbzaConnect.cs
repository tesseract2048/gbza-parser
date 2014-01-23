using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Collections;
using System.IO;

namespace gbzaComm
{
    public class gbzaSection
    {
        public bool isData;
        public byte ID1;
        public byte ID2;
        public int Size1;
        public int Size2;
        public List<gbzaSection> Children = new List<gbzaSection>();
        public byte[] Data;
        public gbzaSection(byte[] data)
        {
            isData = true;
            Data = data;
        }
        public gbzaSection(byte id1, byte id2, int size1)
        {
            isData = false;
            ID1 = id1;
            ID2 = id2;
            Size1 = size1;
            Size2 = 0;
        }
        public gbzaSection(byte id1, byte id2, int size1, int size2)
        {
            isData = false;
            ID1 = id1;
            ID2 = id2;
            Size1 = size1;
            Size2 = size2;
        }
    }

    public class gbzaFormat
    {
        private System.IO.MemoryStream mStream;
        List<gbzaSection> root;
        private BinaryReader bReader;
        public byte[] Attached = null;

        public string[] SetCookie()
        {
            List<string> rtn = new List<string>();
            for (int i = 0; i < root.Count; i++)
            {
                if (root[i].ID1 == 0x16 && root[i].ID2 == 0x00)
                {
                    gbzaSection cookie = root[i];
                    rtn.Add(Encoding.UTF8.GetString(cookie.Children[0].Children[0].Data) + "=" + Encoding.UTF8.GetString(cookie.Children[1].Children[0].Data));
                }
            }
            return rtn.ToArray();
        }

        public string GetError()
        {
            for (int i = 0; i < root.Count; i++)
            {
                if (root[i].ID1 == 0x0D && root[i].ID2 == 0xF0)
                {
                    return Encoding.UTF8.GetString(root[i].Children[0].Children[0].Data);
                }
            }
            return null;
        }

        public string GetPostField(string field)
        {
            for (int i = 0; i < root.Count; i++)
            {
                if (root[i].ID1 == 0x02 && root[i].ID2 == 0xF0)
                {
                    for (int j = 0; j < root[i].Children.Count; j++)
                    {
                        if (root[i].Children[j].ID1 == 0x0A && root[i].Children[j].ID2 == 0xF0)
                        {
                            for (int k = 0; k < root[i].Children[j].Children.Count; k++)
                            {
                                gbzaSection child = root[i].Children[j].Children[k];
                                if (child.ID1 == 0x09 && child.ID2 == 0x00 && child.Children.Count == 2)
                                {
                                    byte[] buf = child.Children[0].Data;
                                    for (int q = 0; q < buf.Length; q++)
                                        if (buf[q] == 0)
                                            buf[q] = 32;
                                    string name = Encoding.UTF8.GetString(buf);
                                    if (name.EndsWith(field) == true)
                                    {
                                        string val = Encoding.Unicode.GetString(child.Children[1].Data);
                                        return val;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        public string GetNewPostField(string field)
        {
            for (int i = 0; i < root.Count; i++)
            {
                if (root[i].ID1 == 0x0A && root[i].ID2 == 0xF0)
                {
                    for (int k = 0; k < root[i].Children.Count; k++)
                    {
                        gbzaSection child = root[i].Children[k];
                        if (child.ID1 == 0x09 && child.ID2 == 0x00 && child.Children.Count == 2)
                        {
                            byte[] buf = child.Children[0].Data;
                            for (int q = 0; q < buf.Length; q++)
                                if (buf[q] == 0)
                                    buf[q] = 32;
                            string name = Encoding.UTF8.GetString(buf);
                            if (name.EndsWith(field) == true)
                            {
                                string val = Encoding.Unicode.GetString(child.Children[1].Data);
                                return val;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private int walkSection(List<gbzaSection> parent, int length)
        {
            int readlen = 0;
            while (readlen < length)
            {
                int curbak = (int)mStream.Position;
                int readbak = readlen;
                if (length - readlen > 4)
                {
                    byte id1 = bReader.ReadByte();
                    byte id2 = bReader.ReadByte();
                    int size1 = bReader.ReadInt16();
                    readlen += 4;
                    int size2 = 0;
                    if (id2 != 0x80)
                    {
                        size2 = bReader.ReadInt32();
                        readlen += 4;
                    }
                    if (size1 + size2 > length - readlen) goto datafield;
                    if (size1 < 0 || size2 < 0) goto datafield;
                    gbzaSection newSection = new gbzaSection(id1, id2, size1, size2);
                    if (mStream.Position + size1 <= mStream.Length) readlen += walkSection(newSection.Children, size1);
                    else goto datafield;
                    if (size2 > 0)
                    {
                        if (mStream.Position + size2 <= mStream.Length) readlen += walkSection(newSection.Children, size2);
                        else goto datafield;
                    }
                    parent.Add(newSection);
                    continue;
                }
            datafield:
                mStream.Position = (long)curbak;
                List<byte> buf = new List<byte>();
                while (readbak < length)
                {
                    byte rd = bReader.ReadByte();
                    buf.Add(rd);
                    readbak++;
                }
                gbzaSection newDataSection = new gbzaSection(buf.ToArray());
                parent.Add(newDataSection);
                readlen = readbak;
            }
            return readlen;
        }
        public gbzaFormat(byte[] _buf)
        {
            byte[] buf = decompressdb1(_buf);
            root = new List<gbzaSection>();
            mStream = new MemoryStream(buf);
            bReader = new BinaryReader(mStream);
            walkSection(root, (int)mStream.Length);
        }

        private byte[] decompressdb1(byte[] _buf)
        {
            MemoryStream mStream = new MemoryStream(_buf);
            System.IO.BinaryReader bReader = new System.IO.BinaryReader(mStream);
            if (bReader.ReadChar() == 'd' && bReader.ReadChar() == 'b' && bReader.ReadChar() == '1')
            {
                mStream.Position = 0x08;
                int filecount = bReader.ReadInt16();
                if (filecount == 1)
                {
                    mStream.Position = 0x0E;
                    int filelen = bReader.ReadInt32();
                    mStream.Position = 0x14;
                    System.IO.Compression.DeflateStream gz = new System.IO.Compression.DeflateStream(mStream, System.IO.Compression.CompressionMode.Decompress);
                    byte[] buf = new byte[filelen];
                    int readlen = gz.Read(buf, 0, filelen);
                    gz.Close();
                    gz.Dispose();
                    bReader.Close();
                    mStream.Close();
                    mStream.Dispose();
                    if (filelen == readlen)
                        return buf;
                    else
                    {
                        bReader.Close();
                        mStream.Close();
                        mStream.Dispose();
                        throw new Exception("File size doesn't match");
                    }
                }
                else
                {
                    mStream.Position = 0x0E;
                    int filelen = bReader.ReadInt16() - 0x18;
                    mStream.Position = 0x18;
                    byte[] _gz = new byte[filelen];
                    mStream.Read(_gz, 0, filelen);
                    MemoryStream _gzStream = new MemoryStream(_gz);
                    System.IO.Compression.DeflateStream gz = new System.IO.Compression.DeflateStream(_gzStream, System.IO.Compression.CompressionMode.Decompress);
                    byte[] _gzbuf = new byte[4096];
                    int readlen = gz.Read(_gzbuf, 0, 4096);
                    gz.Close();
                    gz.Dispose();
                    _gzStream.Close();
                    _gzStream.Dispose();
                    byte[] buf = new byte[readlen];
                    Array.Copy(_gzbuf, buf, readlen);
                    Attached = new byte[(int)mStream.Length - (int)mStream.Position];
                    mStream.Read(Attached, 0, (int)mStream.Length - (int)mStream.Position);
                    bReader.Close();
                    mStream.Close();
                    mStream.Dispose();
                    return buf;
                }
            }
            else
            {
                bReader.Close();
                mStream.Close();
                mStream.Dispose();
                throw new Exception("File format courrupted");
            }
        }
    }

    public class gbzaConnect
    {
        private System.Collections.Specialized.OrderedDictionary Headers = new System.Collections.Specialized.OrderedDictionary();
        private System.Collections.Specialized.OrderedDictionary Fields = new System.Collections.Specialized.OrderedDictionary();
        public int Timeout;
        public string Charset = "utf-8,1,";

        public void AppendHeader(string name, string val)
        {
            Headers[name] = val;
        }

        public void AppendField(string name, string val)
        {
            Fields[name] = val;
        }

        public gbzaFormat SendRequest(string method, string referer, string _url)
        {
            return SendRequest(method, referer, _url, null);
        }

        public gbzaFormat SendRequest(string method, string referer, string _url, string _proxy)
        {
            HttpWebRequest newRequest = HttpWebRequest.Create(_url) as HttpWebRequest;
            newRequest.ServicePoint.Expect100Continue = false;
            newRequest.UserAgent = "ja_176x220";
            newRequest.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            newRequest.KeepAlive = false;
            newRequest.Method = "POST";
            if (_proxy != null && _proxy.Length > 0)
                newRequest.Proxy = new WebProxy(_proxy);
            string header = "";
            Stream post = newRequest.GetRequestStream();
            BinaryWriter postbuf = new BinaryWriter(post);
            byte[] buf;
            string url = _url;
            if (url.IndexOf("?") > -1) url = url.Substring(0, url.IndexOf("?") + 1);
            foreach (DictionaryEntry d in Headers)
            {
                header += (d.Key as string) + ": " + (d.Value as string) + "\r\n";
            }
            if (method == "POST")
            {
                if (Fields.Contains("co")) Charset = ",2,";
                string posthead = referer + "\r\n" + url + "\r\n" + Charset + "\r\n";
                newRequest.ContentType = "application/octet-stream";
                buf = Encoding.UTF8.GetBytes(posthead);
                postbuf.Write((Int32)4);
                postbuf.Write((Int32)buf.Length);
                postbuf.Write(buf);
                buf = Encoding.UTF8.GetBytes(header);
                postbuf.Write((Int32)2);
                postbuf.Write((Int32)buf.Length);
                postbuf.Write(buf);
                if (Fields.Contains("co"))
                {
                    string val = Fields["co"] as string;
                    string cont = "co\r\n" + val.Replace("\r\n", "\n");
                    buf = Encoding.UTF8.GetBytes(cont);
                    postbuf.Write((Int32)0x15);
                    postbuf.Write((Int32)buf.Length);
                    postbuf.Write(buf);
                }
                string postbody = "";
                foreach (DictionaryEntry de in Fields)
                {
                    string name = de.Key as string;
                    string val = de.Value as string;
                    if (name != "co")
                        postbody += name + "=" + val + "\r\n";
                }
                buf = Encoding.UTF8.GetBytes(postbody);
                postbuf.Write((Int32)0x1E);
                postbuf.Write((Int32)buf.Length);
                postbuf.Write(buf);
            }
            else
            {
                string posthead = referer + "\r\n" + url + "\r\n";
                newRequest.ContentType = "application/x-www-form-urlencoded";
                postbuf.Write((Int32)1);
                postbuf.Write((Int32)posthead.Length);
                postbuf.Write(Encoding.UTF8.GetBytes(posthead));
                postbuf.Write((Int32)2);
                postbuf.Write((Int32)header.Length);
                postbuf.Write(Encoding.UTF8.GetBytes(header));
            }
            postbuf.Close();
            post.Close();
            HttpWebResponse response = newRequest.GetResponse() as HttpWebResponse;
            Stream rStream = response.GetResponseStream();
            List<byte> rBuf = new List<byte>();
            byte[] tmp = new byte[4096];
            int readlen;
            while ((readlen = rStream.Read(tmp, 0, 4096)) > 0)
            {
                byte[] tmp2 = new byte[readlen];
                Array.Copy(tmp, tmp2, readlen);
                rBuf.AddRange(tmp2);
            }
            response.Close();
            return new gbzaFormat(rBuf.ToArray());
        }

    }
}
