using Azure.Core;
using RAGSnippetBuilder.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RAGSnippetBuilder.Chroma
{
    public class ChromaWrapper : IDisposable
    {
        #region record: InputArgs

        public record InputArgs
        {
            public InputArg_ID_Vect Snippet { get; init; }
            public InputArg_ID_Vect Description { get; init; }

            // becomes (ID -> ID_XX)
            public InputArg_ID_VectArr Questions { get; init; }
        }

        #endregion
        #region record: InputArg_ID_Vect

        public record InputArg_ID_Vect
        {
            public string ID { get; init; }
            public float[] Vector { get; init; }
        }

        #endregion
        #region record: InputArg_ID_VectArr

        public record InputArg_ID_VectArr
        {
            public string ID { get; init; }
            public float[][] Vectors { get; init; }
        }

        #endregion

        #region record: ChromaAddPost

        private record ChromaAddPost
        {
            public string collection { get; init; }
            public string[] ids { get; init; }
            public float[][] vectors { get; init; }
        }

        #endregion

        #region Declaration Section

        private const string COLLECTION_SNIPPETS = "snippets";
        private const string COLLECTION_DESCRIPTIONS = "descriptions";
        private const string COLLECTION_QUESTIONS = "questions";

        private bool _disposed = false;

        private Process _process = null;
        private string _url = null;

        private readonly object _lock = new object();
        private readonly List<string> _output = new List<string>();

        #endregion

        public ChromaWrapper(string folder_python, string folder_output)
        {
            // TODO: create virtual environment if it doesn't exist

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(folder_python, ".venv", "Scripts", "python.exe"),
                Arguments = Path.Combine(folder_python, "chroma_wrapper.py"),
                WorkingDirectory = folder_output,      // without this, it will create chroma db under the folder that this c# exe is running from
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";       // makes sure print statements go staight to output

            _process = new Process() { StartInfo = startInfo };

            _process.OutputDataReceived += Process_OutputDataReceived;
            _process.ErrorDataReceived += Process_OutputDataReceived;       // just treat error messages as output

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_process != null)
                {
                    _process.Dispose();
                    _process = null;
                }
            }

            _disposed = true;
        }

        public async void Add(InputArgs[] input)
        {
            string url = await WaitForURL();

            // Map input args into what the python script wants
            var snippets = new ChromaAddPost()
            {
                collection = COLLECTION_SNIPPETS,
                ids = input.Select(o => o.Snippet.ID.ToString()).ToArray(),
                vectors = input.Select(o => o.Snippet.Vector).ToArray(),
            };

            var descriptions = new ChromaAddPost()
            {
                collection = COLLECTION_DESCRIPTIONS,
                ids = input.Select(o => o.Description.ID.ToString()).ToArray(),
                vectors = input.Select(o => o.Description.Vector).ToArray(),
            };

            var questions = GetQuestionInput(input);

            // Push to python script
            using (var client = new HttpClient())
            {
                await Add_Post(client, snippets, url);
                await Add_Post(client, descriptions, url);
                await Add_Post(client, questions, url);
            }
        }

        public string[] Ouput
        {
            get
            {
                lock (_lock)
                    return _output.ToArray();
            }
        }

        #region Event Listeners

        // Called each time a line is output, used to look for the url
        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            string line = e.Data;

            if (string.IsNullOrEmpty(line))
                return;

            lock (_lock)
            {
                _output.Add(line);

                if (_url == null)
                {
                    var match = Regex.Match(line, @"\* Running on (?<url>http://\d+.\d+.\d+.\d+:\d+)");
                    if (match.Success)
                        _url = match.Groups["url"].Value;
                }
            }
        }

        #endregion

        #region Private Methods

        private async Task<string> WaitForURL()
        {
            DateTime start = DateTime.UtcNow;

            while(true)
            {
                if ((DateTime.UtcNow - start).TotalSeconds > 120)
                    throw new ApplicationException("Never got url");

                lock (_lock)
                {
                    if (_url != null)
                        return _url;
                }

                await Task.Delay(1000);
            }
        }

        private static async Task Add_Post(HttpClient client, ChromaAddPost request, string url)
        {
            string json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(url + "/add", content);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                string response_text = await response.Content.ReadAsStringAsync();
                throw new ApplicationException($"{response.StatusCode}: {response_text}");
            }
        }

        private static ChromaAddPost GetQuestionInput(InputArgs[] inputs)
        {
            var items = new List<(string id, float[] vect)>();

            foreach (var input in inputs)
            {
                int count = input.Questions.Vectors.Length;
                int str_len = (count - 1).ToString().Length;        // see how many chars the max index will take up (subtracting one in case count is 10 or 100.  the actual max index would be 9 or 99)

                for (int i = 0; i < count; i++)
                {
                    string suffix = i.ToString().PadLeft(str_len, '0');
                    items.Add(($"{input.Questions.ID}_{suffix}", input.Questions.Vectors[i]));
                }
            }

            return new ChromaAddPost()
            {
                collection = COLLECTION_QUESTIONS,
                ids = items.Select(o => o.id).ToArray(),
                vectors = items.Select(o => o.vect).ToArray(),
            };
        }

        #endregion
    }
}
