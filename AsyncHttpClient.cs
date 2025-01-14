﻿using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using httpload.utils;

namespace httpload
{
	internal static class AsyncHttpClient
	{
		public static void TestCreateRequest(Uri uri, HttpMethod method, NameValueCollection headers, bool keepAlive)
		{
			CreateWebRequest(uri, method, headers, keepAlive);
		}

		public static async Task<HttpResult> DoRequestAsync(Uri uri, HttpMethod method, NameValueCollection headers, byte[] data, int timeout, bool keepAlive)
		{
			HttpResult result;
			var stopwatch = new Stopwatch();
			try
			{
				var request = CreateWebRequest(uri, method, headers, keepAlive);

				stopwatch.Start();

				result = await DoRequestAsync(request, data).WithTimeout(timeout, HttpResult.Timeout);
				if(HttpResult.Timeout.Equals(result))
					try { request.Abort(); } catch { }
			}
			catch
			{
				result = HttpResult.Unknown;
			}

			stopwatch.Stop();
			result.ElapsedTicks = stopwatch.Elapsed.Ticks;

			return result;
		}

		private static async Task<HttpResult> DoRequestAsync(HttpWebRequest request, byte[] data)
		{
			if(data == null)
				request.ContentLength = 0L;
			else
			{
				request.ContentLength = data.Length;
				if(data.Length > 0)
				{
					using(var stream = await request.GetRequestStreamAsync())
						await stream.WriteAsync(data, 0, data.Length);
				}
			}

			using(var response = await request.TryGetResponseAsync())
			{
				if(response == null)
					return HttpResult.Unknown;

				var result = new HttpResult {StatusCode = ((HttpWebResponse)response).StatusCode};
				var stream = response.GetResponseStream();
				if(stream != null) result.BytesReceived = await stream.ReadToNull();
				return result;
			}
		}

		private static async Task<WebResponse> TryGetResponseAsync(this WebRequest request)
		{
			try
			{
				return await request.GetResponseAsync();
			}
			catch(WebException we)
			{
				return we.Response;
			}
		}

		private static HttpWebRequest CreateWebRequest(Uri uri, HttpMethod method, NameValueCollection headers, bool keepAlive)
		{
			var request = WebRequest.CreateHttp(uri);
			request.Method = method.ToString();
			request.UserAgent = UserAgent;
			request.AllowReadStreamBuffering = false;
			request.AllowWriteStreamBuffering = false;
			request.KeepAlive = keepAlive;
			if(headers != null && headers.Count > 0)
			{
				string contentType;
				if(!string.IsNullOrEmpty(contentType = headers.Get(ContentTypeHeader)))
				{
					headers.Remove(ContentTypeHeader);
					request.ContentType = contentType;
				}
				request.Headers.Add(headers);
			}
			return request;
		}

		private const string UserAgent = "httpload/1.2";
		private const string ContentTypeHeader = "Content-Type";
	}

	internal struct HttpResult
	{
		public HttpStatusCode StatusCode;
		public long BytesReceived;
		public long ElapsedTicks;

		public static readonly HttpResult Timeout = new HttpResult {StatusCode = (HttpStatusCode)499};
		public static readonly HttpResult Unknown = new HttpResult();
	}
}