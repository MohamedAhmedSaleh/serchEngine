﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using System.Threading;

namespace IR
{
    public partial class Form1 : Form
    {
        private Queue<String> toVisit;//
        private Queue<String> visited;//urls//
        private Queue<String> content;//htmlcontent
        private Queue<String> specificContent; // specificContentFromhtml
        HtmlToText htmltotext;
        int numberOfDocuments;
        string connectionString = "Data source=orcl; User Id=scott; Password=tiger;";
        OracleConnection conn;
        List<String> seeds;
        public Form1()
        {
            InitializeComponent();
            toVisit = new Queue<string>();
            visited = new Queue<string>();
            content = new Queue<string>();
            specificContent = new Queue<string>();
            htmltotext = new HtmlToText();
            numberOfDocuments = 3000;
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            seeds = new List<string>();
            seeds.Add("https://en.wikipedia.org/wiki/Main_Page");
            seeds.Add("https://www.google.com");
        }

        private void crawl_Click(object sender, EventArgs e)
        {
            Parallel.For(0, 2, index =>
            {
                Task firstTask = Task.Factory.StartNew(() => crawler(seeds[index]));
            });

        }

        private void crawler(Object seed)
        {
            toVisit.Enqueue((String)seed);
            

            while (visited.Count != numberOfDocuments)
            {

                if (toVisit.Count != 0)
                {
                    String strToVisit = toVisit.Dequeue();

                    if (!visited.Contains(strToVisit))//to prevent duplicate
                    {
                        string temp = HTTPRequest(strToVisit);//call function to get html

                        if (!temp.Equals(""))
                        {
                            content.Enqueue(temp);
                            visited.Enqueue(strToVisit);
                            String strContent = content.Dequeue();
                            GetSpecificContent(strContent);
                            String strSpecieficContent = specificContent.Dequeue();
                            string[] row = { "", strToVisit, strContent, strSpecieficContent };//
                            ListViewItem lvi = new ListViewItem(row);//

                            if (listView1.InvokeRequired) // We had to do this because we can't access UI objects from a thread
                                listView1.Invoke(new MethodInvoker(delegate
                                {
                                    listView1.Items.Add(lvi);
                                }));
                            else
                                listView1.Items.Add(lvi);

                            searchForLinks(strContent);
                            
                        }

                    }

                }
                if (visitedCount.InvokeRequired)
                    visitedCount.Invoke(new MethodInvoker(delegate
                    {
                        visitedCount.Text = "Visited Pages: " + visited.Count;
                    }));
                else
                    visitedCount.Text = "Visited Pages: " + visited.Count;

            }
            addCrawlerResultsToDatabase();

        }

        public string HTTPRequest(String URL)
        {
            WebRequest myWebRequest;
            WebResponse myWebResponse;

            // Create a new 'WebRequest' object to the mentioned URL.
            Uri uri;
            if (!Uri.TryCreate(URL, UriKind.Absolute, out uri))
                return "";
            string rString;
            try
            {
                myWebRequest = WebRequest.Create(URL);

                // The response object of 'WebRequest' is assigned to a WebResponse' variable.
                myWebResponse = myWebRequest.GetResponse();

                Stream streamResponse = myWebResponse.GetResponseStream();
                StreamReader reader = new StreamReader(streamResponse);
                rString = reader.ReadToEnd();

                streamResponse.Close();
                reader.Close();
                myWebResponse.Close();
            }
            catch (Exception ex)
            {
                return "";
            }
            return rString;

        }
        public void searchForLinks(String content)
        {
            if (toVisit.Count < numberOfDocuments + 500)
            {
                var urlDictionary = new Dictionary<string, string>();

                Match match = Regex.Match(content, "(?i)<a .*?href=\"([^\"]+)\"[^>]*>(.*?)</a>");
                while (match.Success)
                {
                    string urlKey = match.Groups[1].Value;
                    string urlValue = Regex.Replace(match.Groups[2].Value, "(?i)<.*?>", string.Empty);
                    urlDictionary[urlKey] = urlValue;
                    match = match.NextMatch();
                }

                foreach (var item in urlDictionary)
                {
                    string href = item.Key;
                    string text = item.Value;
                    if (!string.IsNullOrEmpty(href))
                    {
                        string url = href.Replace("%3f", "?")
                            .Replace("%3d", "=")
                            .Replace("%2f", "/")
                            .Replace("&amp;", "&");

                        if (string.IsNullOrEmpty(url) || url.StartsWith("#")
                            || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                            || url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if ((url.Contains("http://") || url.Contains("https://")) && (!toVisit.Contains(url)))
                            toVisit.Enqueue(url);
                    }
                }
            }
        }

        private void GetSpecificContent(string htmlContent)
        {
            specificContent.Enqueue(htmltotext.ConvertHtml(htmlContent));
        }
        private void addCrawlerResultsToDatabase()
        {
            conn = new OracleConnection(connectionString);
            conn.Open();
            int count = 0;
            OracleCommand cmd;

            while (count < visited.Count)
            {
                cmd = new OracleCommand();
                cmd.Connection = conn;
                cmd.CommandText = "INSERT_NEW_CRAWLER_RESULT";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("page_url", OracleDbType.Varchar2, DBNull.Value, ParameterDirection.Input).Value = visited.ElementAt(count);
                cmd.Parameters.Add("page_content", OracleDbType.NClob, DBNull.Value, ParameterDirection.Input).Value = content.ElementAt(count);
                cmd.Parameters.Add("page_specific_content", OracleDbType.NClob, DBNull.Value, ParameterDirection.Input).Value = specificContent.ElementAt(count);
                cmd.ExecuteNonQuery();
                count++;
            }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }
    }
}