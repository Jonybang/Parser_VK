using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using System.Web;

namespace DataDeserializator
{
    public class DataDeserializer<T>
    {
        public bool XmlSerialize(T obj, string fileName, bool isDeleteBeforeSerialize, string pathToFile = "")
        {

            pathToFile = HttpContext.Current.Server.MapPath("~/data/");
            if (pathToFile != "")
                if (!(Directory.Exists(pathToFile)))
                    Directory.CreateDirectory(pathToFile);

            XmlSerializer sr = new XmlSerializer(typeof(T));
            //XmlSerializer sr = new XmlSerializer(typeof(T));
            
            if (isDeleteBeforeSerialize)
                if (Directory.Exists(pathToFile + "\\" + fileName + ".xml"))
                    Directory.Delete(pathToFile + "\\" + fileName + ".xml");

            FileStream fs = new FileStream(pathToFile + "\\" + fileName + ".xml", FileMode.Create);
            try
            {
            XmlTextWriter xmlOut = new XmlTextWriter(fs, Encoding.UTF8);
            sr.Serialize(xmlOut, obj);
            xmlOut.Close();
            fs.Close();
            }
            catch
            {
                fs.Close();
                fs.Dispose();
                return false;
            }
            return true;           
        }

        public T XmlDeserialize(string fileName, string pathToFile = "")
        {
            pathToFile = HttpContext.Current.Server.MapPath("~/data/");
            if (pathToFile != "")
                if (!(Directory.Exists(pathToFile)))
                    Directory.CreateDirectory(pathToFile);

            FileStream fs = new FileStream(pathToFile + "\\" + fileName + ".xml", FileMode.OpenOrCreate);
            XmlTextReader xmlIn = new XmlTextReader(fs);
            T obj;
            // десериализуем 
            try
            {
                XmlSerializer dsr = new XmlSerializer(typeof(T));
                obj = (T)dsr.Deserialize(xmlIn);
                fs.Close();
                fs.Dispose();
                return obj;
            }
            catch (Exception e)
            {
                e.ToString();
                fs.Close();
                fs.Dispose();
                return default(T);
            }
        }
    }
}

