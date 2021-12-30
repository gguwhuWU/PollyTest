using JitterMagic;
using Polly;
using Polly.Fallback;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace PollyTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            test_PollyRetry_JitterStrategy();

            Console.WriteLine("結束退出");
            Console.ReadKey();
        }

        /// <summary>
        /// https://marcus116.blogspot.com/2019/06/polly-jitter-strategy-in-aspnetcore.html
        /// </summary>
        private static void test_PollyRetry_JitterStrategy()
        {
            Random jitterer = new Random();
            Policy
                 .Handle<HttpRequestException>()
                 .OrResult<HttpResponseMessage>(result => result.StatusCode != HttpStatusCode.OK)
                 .WaitAndRetry(5,
                     retryAttempt =>
                         TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                                     + TimeSpan.FromMilliseconds(jitterer.Next(0, 100))
                 )
                 .Execute(doMockHTTPRequest);
        }

        private static void test_jitter()
        {
            for (int i = 0; i < 10; i++)
            {
                Console.Write(Jitter.Apply(100, new JitterSettings(87)) + ",");
            }
        }

        /// <summary>
        /// https://marcus116.blogspot.com/2019/06/netcore-polly-timeout-wrap.html
        /// </summary>
        private static void test5()
        {
            var timeoutPolicys = Policy
         .Timeout(TimeSpan.FromMilliseconds(1),
             onTimeout: (context, timespan, task) =>
             {
                 Console.WriteLine($"{context.PolicyKey} : execution timed out after {timespan} seconds.");
             });

            RetryPolicy waitAndRetryPolicy = Policy
                .Handle<Exception>()
                .Retry(3,
                    onRetry: (exception, retryCount) =>
                    {
                        Console.WriteLine($"[Polly retry] : 呼叫 API 異常, 進行第 {retryCount} 次重試");
                    });

            FallbackPolicy<String> fallbackForTimeout = Policy<String>
                .Handle<TimeoutRejectedException>()
                .Fallback(
                    fallbackValue: "Please try again later [Fallback for timeout]",
                    onFallback: b => { Console.WriteLine($"這個請求超時了耶"); }
                );

            FallbackPolicy<String> fallbackForAnyException = Policy<String>
                .Handle<Exception>()
                .Fallback(
                    fallbackAction: () => { return "Please try again later [Fallback for any exception]"; },
                    onFallback: e => { Console.WriteLine($"[Polly fallback] : 重試失敗, say goodbye"); }
                );

            PolicyWrap<String> policyWrap = fallbackForAnyException.Wrap(fallbackForTimeout).Wrap(waitAndRetryPolicy)
                .Wrap(timeoutPolicys);
            policyWrap.Execute(() => doMockHTTPRequest2());
        }

        static string doMockHTTPRequest2()
        {
            Console.WriteLine($"開始發送 Request");

            HttpResponseMessage result;
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(3);
                result = client.GetAsync("http://www.mocky.io/v2/5cfb4d9b3000006e080a8b0a").Result;
            }

            return result.Content.ReadAsStringAsync().Result;
        }

        private static void test1()
        {
            Policy
          // 故障處理 : 要 handle 什麼樣的異常
          // Multiple exception types
          .Handle<HttpRequestException>()
          .Or<OperationCanceledException>()
          .OrResult<HttpResponseMessage>(result => result.StatusCode != HttpStatusCode.OK)
          // 重試策略 : 異常發生時要進行的重試次數及重試機制
          .Retry(3, onRetry: (exception, retryCount) =>
          {
              Console.WriteLine($"[App|Polly] : 呼叫 API 異常, 進行第 {retryCount} 次重試, Error :{exception.Result.StatusCode}");
          })
          // 要執行的任務
          .Execute(doMockHTTPRequest);
        }

        private static void test4()
        {
            var policy = Policy.Handle<WebException>(wex =>
             wex.Status == WebExceptionStatus.ProtocolError &&
             ((HttpWebResponse)wex.Response).StatusCode
                 == HttpStatusCode.ServiceUnavailable)
             .Retry(3);

                var succCount = Enumerable.Range(1, 1).Select((i) =>
                {
                    try
                    {
                        var r = policy.Execute(() => doMockHTTPRequest());
                        return r.IsSuccessStatusCode;
                    }
                    catch (Exception ex)
                    {
                        return false;
                    }
                }).Where(o => o).Count();
                Console.WriteLine(succCount);
        }

        private static void test3()
        {
            Policy
          // 故障處理 : 要 handle 什麼樣的異常
          // Multiple exception types
          .Handle<HttpRequestException>()
          .Or<OperationCanceledException>()
          .OrResult<HttpResponseMessage>(result => result.StatusCode != HttpStatusCode.OK)
          .WaitAndRetry(5, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
              {
                  Console.WriteLine($"[App|Polly] : 呼叫 API 異常, 第 {retryCount} 次 失敗，等待 {timespan} 秒後重試, Error :{outcome.Result.StatusCode}");
              })
          // 要執行的任務
          .Execute(doMockHTTPRequest);
        }

        private static void test2()
        {
            Policy
          // 故障處理 : 要 handle 什麼樣的異常
          // Multiple exception types
          .Handle<HttpRequestException>()
          .Or<OperationCanceledException>()
          .OrResult<HttpResponseMessage>(result => result.StatusCode != HttpStatusCode.OK)
          .WaitAndRetry(new[]
          {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(3)
          }, onRetry: (exception, time) =>
          {
              Console.WriteLine($"[App|Polly] : 呼叫 API 異常, 等待 {time} 秒後重試, Error :{exception.Result.StatusCode}");
          })
          // 要執行的任務
          .Execute(doMockHTTPRequest);
        }

        static HttpResponseMessage doMockHTTPRequest()
        {
            Console.WriteLine($"[App] {DateTime.Now.ToString(CultureInfo.InvariantCulture)}: 開始發送 Request");

            HttpResponseMessage result;
            using (HttpClient client = new HttpClient())
            {
                result = client.GetAsync("http://www.mocky.io/v2/5cfb4d9b3000006e080a8b0a").Result;
            }

            return result;
        }
    }
}
