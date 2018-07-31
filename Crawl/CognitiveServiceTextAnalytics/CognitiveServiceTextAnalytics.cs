//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Microsoft.DecisionService.Crawl
{
    public class CognitiveServiceTextAnalytics
    {
        private static readonly CognitiveService cogService;

        static CognitiveServiceTextAnalytics()
        {
            cogService = new CognitiveService("CogTextAnalytics", queryParams: "/sentiment");
        }

        internal static TextAnalyticRequest CreateRequestFromText(string text)
        {
            // Based on email thread with Arvind Krishnaa Jagannathan <arjagann@microsoft.com>
            if (text.Length >= 10240 / 2)
            text = text.Substring(0, 10240 / 2);

            return new TextAnalyticRequest
            {
                Documents = new List<TextAnalyticDocument>
                {
                    new TextAnalyticDocument
                    {
                        //Language = "english",
                        Text = text,
                        Id = "1"
                    }
                }
            };
        }

        public static Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log, CancellationToken cancellationToken)
        {
            return cogService.InvokeAsync(req, log, 
                reqBody =>
                {
                    var textBuilder = new StringBuilder();

                    if (!string.IsNullOrEmpty(reqBody.Title))
                        textBuilder.AppendLine(reqBody.Title);
                    if (!string.IsNullOrEmpty(reqBody.Article))
                        textBuilder.AppendLine(reqBody.Article);

                    if (textBuilder.Length == 0)
                        return null;

                    var text = textBuilder.ToString();

                    // Based on email thread with Arvind Krishnaa Jagannathan <arjagann@microsoft.com>
                    if (text.Length >= 10240 / 2)
                        text = text.Substring(0, 10240 / 2);

                    return CreateRequestFromText(text);
                },
                (reqBody, blobContent) =>
                {
                    blobContent.Output = new JObject();

                    var responseObj = JsonConvert.DeserializeObject<TextAnalyticResponse<DocumentSentiment>>(blobContent.Value);
                    if (responseObj?.Documents?.Length == 1)
                        blobContent.Output.Add(new JProperty("XSentiment", responseObj.Documents[0].Score));
                },
                isPost: true,
                cancellationToken: cancellationToken);
        }

        public class TextAnalyticRequest
        {
            [JsonProperty("documents")]
            public List<TextAnalyticDocument> Documents { get; set; }
        }

        public class TextAnalyticDocument
        {
            [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
            public string Language { get; set; }

            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("text")]
            public string Text { get; set; }
        }


        public class TextAnalyticResponse<TDocumentResult> where TDocumentResult : TextAnalyticDocumentResultBase
        {
            [JsonProperty("documents")]
            public TDocumentResult[] Documents { get; set; }

            [JsonProperty("errors")]
            public TextAnalyticResponseError[] Errors { get; set; }
        }

        public class TextAnalyticDocumentResultBase
        {
            [JsonProperty("id")]
            public string Id { get; set; }
        }

        public class DocumentSentiment : TextAnalyticDocumentResultBase
        {
            [JsonProperty("score")]
            public float Score { get; set; }
        }

        public class TextAnalyticResponseError
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }
        }
    }
}
