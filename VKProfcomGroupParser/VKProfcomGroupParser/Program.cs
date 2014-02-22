using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace VKProfcomGroupParser
{
    class Program
    {
        //https://oauth.vk.com/authorize?client_id=4199799&scope=328705&redirect_uri=http://oauth.vk.com/blank.html&display=page&v=5.10&response_type=token
        static string acces_token = "DELETE";
        //https://oauth.vk.com/access_token?client_id=4199567&client_secret=DELETE&v=5.10&grant_type=client_credentials
        static string notifications_acces_token = "DELETE";

        static int groupID = -66470865;

        static string emptyAnswer = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<response/>\n";
        static int lastID = -1;
        static int minutes = 0;

        static void Main(string[] args)
        {
            Thread t = new Thread(SearchAndAddNewPostFromVK);
            t.Start();     
        }
        private static void SearchAndAddNewPostFromVK()
        {
            for ( ; ; )
            {
                WebClient client = new WebClient();
                client.Encoding = System.Text.Encoding.UTF8;

                client.DownloadStringCompleted += (sender, args) =>
                {
                    NameValueCollection json = client.QueryString;
                    if (!args.Cancelled && args.Error == null)
                    {
                        string result = args.Result; // do something fun...
                        Parsing(result);
                    }
                };
                client.DownloadStringAsync(new Uri("https://api.vk.com/method/wall.get.xml?owner_id=" + groupID + "&count=1&access_token=" + acces_token));
                Console.WriteLine("=========================================================");
                Thread.Sleep(60000);
                Console.WriteLine("Минута прошла\n");
            }
        }
        private static void Parsing(string result)
        {
            //Console.WriteLine("\n" + result);
            var response = new XmlDocument();

            response.Load(XmlReader.Create(new StringReader(result)));
            foreach (XmlNode node in response.SelectNodes("response"))
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name != "count")
                    {
                        int postId = Convert.ToInt32(child["id"].InnerText);

                        Console.WriteLine("PostID == " + postId);

                        if (postId == lastID || IsIDExistInDB(postId))
                        {
                            Console.WriteLine("Содержится в базе данных");
                            return;
                        }
                        else Console.WriteLine("Отсутствует в базе данных");

                        string postContents = child["text"].InnerText;

                        if (postContents.IndexOf("#Уведомление") == -1)
                        {
                            Console.WriteLine("Отсутствует хештег, сброс, ожидание - 1 минута");
                            continue;
                        }

                        string textToSend = CutTheString(postContents) + " подробнее: http://vk.com/club" + Math.Abs(groupID) + "?w=wall" + groupID + "_" + postId;

                        Console.WriteLine("Текст отправки: \n" + textToSend + "\n");

                        //Изображение, на будущее
                        try
                        {
                            string imageURL = child["attachment"].SelectSingleNode("photo")["src_big"].InnerText;
                        }
                        catch (NullReferenceException)
                        {
                            Console.WriteLine("Изображение отсутствует\n");
                        }
                        WebClient client = new WebClient();

                        //Отправка уведомления
                        string request = "https://api.vk.com/method/secure.sendNotification.xml?user_ids="+GetUsersList()+"&message=" + textToSend + "&access_token=" + notifications_acces_token + "&client_secret=wRc7uTkO8RqOJfzJn5q1";
                        string answer = client.DownloadString(new Uri(request));
                        Console.WriteLine("Текст ответа на запрос:\n \n" + answer);

                        if (answer == emptyAnswer)
                        {
                            Console.WriteLine("Уведомление никому не отправленно, так как прошлое было \nотправленно меньше чем час назад или это 4-е уведомление \nза день(максимум 3)");
                            Console.WriteLine(minutes + " минут с первой попытки.");
                            minutes++;
                            return;
                        }

                        response.Load(XmlReader.Create(new StringReader(answer)));
                        try
                        {
                            XmlElement xmlEl = response["error"];
                            Console.WriteLine("Ошибка, проверка кода ошибки");
                            if (xmlEl["error_code"].InnerText == "15")
                                Console.WriteLine("Код ошибки 15: Нельзя отправлять уведомления, чаще чем раз в час.");
                            else
                                Console.WriteLine("Неизвестная ошибка под номером " + xmlEl["error_code"].InnerText);

                            Console.WriteLine("Сброс");
                            return;
                        }
                        catch (NullReferenceException)
                        {
                            Console.WriteLine("Ошибок нет");
                        }

                        Console.WriteLine("Уведомление отправлено");
                        Console.WriteLine("Запись в базу данных");

                        saveToDb(postId);
                        lastID = postId;
                        minutes = 0;
                    }
                }
            }
        }

        private static bool IsIDExistInDB(int id)
        {
            string pathToFile=Environment.CurrentDirectory + "\\DB.xml";
            if (!File.Exists(pathToFile))
            {
                return false;
            }
            else
            {
                var DB = new XmlDocument();
                DB.Load(pathToFile);
                int n = 0;
                foreach (XmlNode node in DB.SelectNodes("ID_DB"))
                {
                    if (n > 5) break; else n++;

                    int postId = Convert.ToInt32(node["ID"].InnerText);

                    if (postId == id)
                        return true;
                }
                return false;
            }                
        }

        private static string CutTheString(string postContents)
        {
            postContents = postContents.Replace("<br>", "");

            if (!String.IsNullOrEmpty(postContents))
            {
                if (postContents.Count() > 45) { 
                    postContents = postContents.Remove(45); 
                    postContents += "..."; }
                return postContents;
            }
            else return "[Текст в новосте отсутствует] ";
        }

        private static string GetUsersList()
        {
            var response = new XmlDocument();
            WebClient client = new WebClient();
            //Участники сообщества
            string request = "https://api.vk.com/method/groups.getMembers.xml?group_id=" + Math.Abs(groupID) + "&access_token=" + acces_token;
            string answer = client.DownloadString(new Uri(request));
            response.Load(XmlReader.Create(new StringReader(answer)));
            XmlElement users = response["response"]["users"];
            var usersList = users.SelectNodes("uid");
            string usersListForSend = "";
            foreach (XmlElement user in usersList)
                usersListForSend += user.InnerText + ",";
            usersListForSend = usersListForSend.Substring(0, usersListForSend.Length - 1);
            return usersListForSend;
        }

        private static void saveToDb(int id)
        {
            var DB = new XmlDocument();
            string pathToFile=Environment.CurrentDirectory + "\\DB.xml";
            if (!File.Exists(pathToFile))
            {
                DB.LoadXml("<ID_DB></ID_DB>");
                XmlNode xmlNode = DB.CreateElement("ID");
                xmlNode.InnerText = id.ToString();
                DB.DocumentElement.PrependChild(xmlNode);
                DB.Save(pathToFile);
            }
            else
            {
                DB.Load(pathToFile);
                XmlNode xmlNode = DB.CreateElement("ID");
                xmlNode.InnerText = id.ToString();
                DB.DocumentElement.PrependChild(xmlNode);
                DB.Save("DB.xml");
            }
        }
    }
}
