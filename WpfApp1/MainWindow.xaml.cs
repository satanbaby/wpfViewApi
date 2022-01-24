using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Dynamic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            List<ComboBoxPairs> cbp = new List<ComboBoxPairs>();

            cbp.Add(new ComboBoxPairs("陸軍API", "http://localhost:51739/"));
            cbp.Add(new ComboBoxPairs("空軍API", ""));
            cbp.Add(new ComboBoxPairs("台船隆德", ""));
            ip.DisplayMemberPath = "_Key";
            ip.SelectedValuePath = "_Value";
            ip.ItemsSource = cbp;
            ip.SelectedIndex = 0;
        }

        public class ComboBoxPairs
        {
            public string _Key { get; set; }
            public string _Value { get; set; }

            public ComboBoxPairs(string _key, string _value)
            {
                _Key = _key;
                _Value = _value;
            }
        }

        /// <summary>
        /// TrimmingConverter
        /// </summary>
        private class TrimmingConverter : JsonConverter
        {
            /// <summary>
            /// CanRead
            /// </summary>
            public override bool CanRead => true;
            /// <summary>
            /// CanWrite
            /// </summary>
            public override bool CanWrite => false;
            /// <summary>
            /// CanConvert
            /// </summary>
            /// <param name="objectType">type</param>
            /// <returns>data</returns>
            public override bool CanConvert(Type objectType) => objectType == typeof(string);
            /// <summary>
            /// ReadJson
            /// </summary>
            /// <param name="reader">reader</param>
            /// <param name="objectType">objectType</param>
            /// <param name="existingValue">existingValue</param>
            /// <param name="serializer">serializer</param>
            /// <returns>data</returns>
            public override object ReadJson(JsonReader reader, Type objectType,
                                            object existingValue, JsonSerializer serializer)
            {
                if (reader.ValueType != typeof(string))
                {
                    return reader.Value?.ToString();
                }

                return ((string)reader.Value)?.Trim();
            }
            /// <summary>
            /// WriteJson
            /// </summary>
            /// <param name="writer">writer</param>
            /// <param name="value">value</param>
            /// <param name="serializer">serializer</param>
            public override void WriteJson(JsonWriter writer, object value,
                                           JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(url.Text))
            {
                MessageBox.Show("請輸入API路徑");
                return;
            }
            var apiList = GetApi().ToList();
            var apiProperty = new List<string>();
            foreach (var element in (apiList.First() as JObject))
            {
                apiProperty.Add(element.Key);
            }
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                using (var tran = connection.BeginTransaction())
                {
                    var createSql = $@"CREATE TABLE Temp ({string.Join(",", apiProperty.Select(_ => $"{_} varchar(255)"))})";
                    connection.Execute(createSql);
                    //connection.Execute("create table Person as (@list)", list);
                    var insertSql = $"insert into Temp values ({string.Join(", ", apiProperty.Select(_ => $"@{_}"))})";
                    foreach (var item in apiList)
                    {
                        var parmeter = new DynamicParameters();
                        foreach (var i in item as JObject)
                        {
                            parmeter.Add("@" + i.Key, i.Value.ToString());
                        }

                        connection.Execute(insertSql, parmeter);
                    }
                    tran.Commit();
                    var query = connection.Query(!string.IsNullOrEmpty(searchModel.Text) ? searchModel.Text : "select * from Temp").ToList();

                    dataGrid.ItemsSource = query;
                }
            }
        }

        private IEnumerable<dynamic> GetApi()
        {
            var httppath = ip.SelectedValue + url.Text;
            var model = body.Text;
            var handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip;
            try
            {
                //建立 HttpClient
                using (var client = new HttpClient(handler))
                using (var contentPost = new StringContent(model, Encoding.UTF8, "application/json"))
                using (var response = client.PostAsync(httppath, contentPost).Result)
                using (var stream = response.Content.ReadAsStreamAsync().Result)
                using (var sr = new StreamReader(stream))
                using (var reader = new JsonTextReader(sr))
                {
                    var setting = new JsonSerializerSettings();
                    setting.Converters.Add(new TrimmingConverter());
                    JsonSerializer serializer = JsonSerializer.Create(setting);

                    //判斷 StatusCode
                    if (response.StatusCode != HttpStatusCode.BadRequest)
                    {
                        var result = serializer
                           .Deserialize<IEnumerable<dynamic>>(reader);
                        //.Where("name==\"Ben\"")
                        return result;

                    }
                    else
                    {
                        // 將回應結果內容取出並轉為 string 再透過 BadRequest 輸出
                        var responseMessage = response.Content.ReadAsStringAsync().Result;
                        throw new Exception(responseMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                return new List<object> { new { error = ex.Message.ToString() } };
            }
        }

        public class Person
        {
            public string Rdate { get; set; }
            public string date { get; set; }
            public string time { get; set; }
            public string count { get; set; }
            public string name { get; set; }
            public string tel { get; set; }
            public string mail { get; set; }
        }
    }
}
