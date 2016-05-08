﻿//******************************************************************************************************
//  WebPageController.cs - Gbtc
//
//  Copyright © 2016, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  01/12/2016 - Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using GSF.Data;
using GSF.IO;
using GSF.IO.Checksums;
using GSF.Reflection;
using GSF.Web.Model;

namespace GSF.Web.Hosting
{
    /// <summary>
    /// Defines a mini-web server with Razor support using the self-hosted API controller.
    /// </summary>
    public class WebPageController : ApiController
    {
        #region [ Members ]

        // Events

        /// <summary>
        /// Exposes execution exceptions that occur in <see cref="WebPageController"/> instances.
        /// </summary>
        public event EventHandler<Exception> ExecutionException;

        /// <summary>
        /// Exposes status messages that get reported from <see cref="WebPageController"/> instances.
        /// </summary>
        public event EventHandler<string> StatusMessage;

        // Fields
        private readonly string m_webRootPath;
        private readonly IRazorEngine m_razorEngineCS;
        private readonly IRazorEngine m_razorEngineVB;
        private readonly ConcurrentDictionary<string, uint> m_etagCache;
        private readonly SafeFileWatcher m_fileWatcher;
        private bool m_disposed;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new <see cref="WebPageController"/>.
        /// </summary>
        /// <param name="webRootPath">Root path for web page; defaults to template path for <paramref name="razorEngineCS"/>.</param>
        /// <param name="razorEngineCS">Razor engine instance for .cshtml templates; uses default instance if not provided.</param>
        /// <param name="razorEngineVB">Razor engine instance for .vbhtml templates; uses default instance if not provided.</param>
        public WebPageController(string webRootPath = null, IRazorEngine razorEngineCS = null, IRazorEngine razorEngineVB = null)
        {
            bool releaseMode = !AssemblyInfo.EntryAssembly.Debuggable;
            m_razorEngineCS = razorEngineCS ?? (releaseMode ? RazorEngine<CSharp>.Default : RazorEngine<CSharpDebug>.Default as IRazorEngine);
            m_razorEngineVB = razorEngineVB ?? (releaseMode ? RazorEngine<VisualBasic>.Default : RazorEngine<VisualBasicDebug>.Default as IRazorEngine);
            m_webRootPath = FilePath.AddPathSuffix(webRootPath ?? m_razorEngineCS.TemplatePath);
            m_etagCache = new ConcurrentDictionary<string, uint>(StringComparer.InvariantCultureIgnoreCase);

            m_fileWatcher = new SafeFileWatcher(m_webRootPath)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            m_fileWatcher.Changed += m_fileWatcher_FileChange;
            m_fileWatcher.Deleted += m_fileWatcher_FileChange;
            m_fileWatcher.Renamed += m_fileWatcher_FileChange;

            DefaultWebPage = "index.html";
            ClientCacheEnabled = true;
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets or sets default web page to use for this <see cref="WebPageController"/>.
        /// </summary>
        public string DefaultWebPage
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the <see cref="RazorView"/> model instance for this <see cref="WebPageController"/>, if any.
        /// </summary>
        public object RazorModel
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the <see cref="RazorView"/> model <see cref="Type"/> for this <see cref="WebPageController"/>, if any.
        /// </summary>
        public Type RazorModelType
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets data connection to provide to <see cref="RazorView"/> instances in this <see cref="WebPageController"/>, if any.
        /// </summary>
        public AdoDataConnection RazorConnection
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets flag that determines if cache control is enabled for browser clients; default to <c>true</c>.
        /// </summary>
        public bool ClientCacheEnabled
        {
            get;
            set;
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="WebPageController"/> object and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                try
                {
                    if (disposing)
                        m_fileWatcher?.Dispose();
                }
                finally
                {
                    m_disposed = true;          // Prevent duplicate dispose.
                    base.Dispose(disposing);    // Call base class Dispose().
                }
            }
        }

        /// <summary>
        /// Default page request handler.
        /// </summary>
        /// <returns></returns>
        [Route, HttpGet]
        public Task<HttpResponseMessage> GetPage()
        {
            return GetPage(DefaultWebPage);
        }

        /// <summary>
        /// Common page request handler.
        /// </summary>
        /// <param name="pageName">Page name to render.</param>
        /// <returns>Rendered page result for given page.</returns>
        [Route("{pageName}"), HttpGet]
        public Task<HttpResponseMessage> GetPage(string pageName)
        {
            return RenderResponse(pageName);
        }

        /// <summary>
        /// Common post request handler.
        /// </summary>
        /// <param name="pageName">Page name to render.</param>
        /// <param name="postData">Post data.</param>
        /// <returns>Rendered page result for given page.</returns>
        [Route("{pageName}"), HttpPost]
        public Task<HttpResponseMessage> PostPage(string pageName, dynamic postData)
        {
            return RenderResponse(pageName, postData);
        }

        private async Task<HttpResponseMessage> RenderResponse(string pageName, dynamic postData = null)
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);

            string content, fileExtension = FilePath.GetExtension(pageName).ToLowerInvariant();

            switch (fileExtension)
            {
                case ".cshtml":
                    content = await new RazorView(m_razorEngineCS, pageName, RazorModel, RazorModelType, RazorConnection, OnExecutionException).ExecuteAsync(Request, postData);
                    response.Content = new StringContent(content, Encoding.UTF8, "text/html");
                    break;
                case ".vbhtml":
                    content = await new RazorView(m_razorEngineVB, pageName, RazorModel, RazorModelType, RazorConnection, OnExecutionException).ExecuteAsync(Request, postData);
                    response.Content = new StringContent(content, Encoding.UTF8, "text/html");
                    break;
                default:
                    string fileName = FilePath.GetAbsolutePath($"{m_webRootPath}{pageName.Replace('/', Path.DirectorySeparatorChar)}");

                    if (File.Exists(fileName))
                    {
                        FileStream fileData = null;
                        uint responseHash = 0;

                        if (ClientCacheEnabled && !m_etagCache.TryGetValue(fileName, out responseHash))
                        {
                            // Calculate check-sum for file
                            await Task.Run(() =>
                            {
                                const int BufferSize = 32768;
                                byte[] buffer = new byte[BufferSize];
                                Crc32 calculatedHash = new Crc32();

                                fileData = File.OpenRead(fileName);
                                int bytesRead = fileData.Read(buffer, 0, BufferSize);

                                while (bytesRead > 0)
                                {
                                    calculatedHash.Update(buffer, 0, bytesRead);
                                    bytesRead = fileData.Read(buffer, 0, BufferSize);
                                }

                                responseHash = calculatedHash.Value;
                                m_etagCache.TryAdd(fileName, responseHash);
                                fileData.Seek(0, SeekOrigin.Begin);

                                OnStatusMessage($"Cache [{responseHash}] added for file \"{fileName}\"");
                            });
                        }

                        if (PublishResponseContent(response, responseHash))
                        {
                            if (fileData == null)
                                fileData = File.OpenRead(fileName);

                            response.Content = await Task.Run(() => new StreamContent(fileData));
                            response.Content.Headers.ContentType = new MediaTypeHeaderValue(MimeMapping.GetMimeMapping(pageName));
                        }
                        else
                        {
                            fileData?.Dispose();
                        }
                    }
                    else
                    {
                        response.StatusCode = HttpStatusCode.NotFound;
                    }
                    break;
            }

            return response;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PublishResponseContent(HttpResponseMessage response, long responseHash)
        {
            if (!ClientCacheEnabled)
                return true;

            // See if client's version of cached resource is up to date
            foreach (EntityTagHeaderValue headerValue in Request.Headers.IfNoneMatch)
            {
                long requestHash;

                if (long.TryParse(headerValue.Tag?.Substring(1, headerValue.Tag.Length - 2), out requestHash) && responseHash == requestHash)
                {
                    response.StatusCode = HttpStatusCode.NotModified;
                    return false;
                }
            }

            response.Headers.CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = new TimeSpan(31536000 * TimeSpan.TicksPerSecond)
            };

            response.Headers.ETag = new EntityTagHeaderValue($"\"{responseHash}\"");
            return true;
        }

        private void OnExecutionException(Exception exception)
        {
            ExecutionException?.Invoke(this, exception);
        }

        private void OnStatusMessage(string message)
        {
            StatusMessage?.Invoke(this, message);
        }

        private void m_fileWatcher_FileChange(object sender, FileSystemEventArgs e)
        {
            uint responseHash;

            if (m_etagCache.TryRemove(e.FullPath, out responseHash))
                OnStatusMessage($"Cache [{responseHash}] cleared for file \"{e.FullPath}\"");
        }

        #region [ Sub-folder Handlers ]

        /// <summary>
        /// Sub-folder request handler - depth 1.
        /// </summary>
        /// <param name="folder1">First folder.</param>
        /// <param name="pageName">Page name to render.</param>
        /// <returns>Rendered page result for given page and path.</returns>
        [Route("{folder1}/{pageName}"), HttpGet]
        public Task<HttpResponseMessage> GetPage(string folder1, string pageName)
        {
            return GetPage($"{folder1}/{pageName}");
        }

        /// <summary>
        /// // Sub-folder post handler - depth 1.
        /// </summary>
        /// <param name="folder1">First folder.</param>
        /// <param name="pageName">Page name to render.</param>
        /// <param name="postData">Post data.</param>
        /// <returns>Rendered page result for given page and path.</returns>
        [Route("{folder1}/{pageName}"), HttpPost]
        public Task<HttpResponseMessage> PostPage(string folder1, string pageName, dynamic postData)
        {
            return PostPage($"{folder1}/{pageName}", postData);
        }

        /// <summary>
        /// Sub-folder request handler - depth 2.
        /// </summary>
        /// <param name="folder1">First folder.</param>
        /// <param name="folder2">Second folder.</param>
        /// <param name="pageName">Page name to render.</param>
        /// <returns>Rendered page result for given page and path.</returns>
        [Route("{folder1}/{folder2}/{pageName}"), HttpGet]
        public Task<HttpResponseMessage> GetPage(string folder1, string folder2, string pageName)
        {
            return GetPage($"{folder1}/{folder2}/{pageName}");
        }

        /// <summary>
        /// Sub-folder post handler - depth 2.
        /// </summary>
        /// <param name="folder1">First folder.</param>
        /// <param name="folder2">Second folder.</param>
        /// <param name="pageName">Page name to render.</param>
        /// <param name="postData">Post data.</param>
        /// <returns>Rendered page result for given page and path.</returns>
        [Route("{folder1}/{folder2}/{pageName}"), HttpPost]
        public Task<HttpResponseMessage> PostPage(string folder1, string folder2, string pageName, dynamic postData)
        {
            return PostPage($"{folder1}/{folder2}/{pageName}", postData);
        }

        /// <summary>
        /// Sub-folder request handler - depth 3.
        /// </summary>
        /// <param name="folder1">First folder.</param>
        /// <param name="folder2">Second folder.</param>
        /// <param name="folder3">Third folder.</param>
        /// <param name="pageName">Page name to render.</param>
        /// <returns>Rendered page result for given page and path.</returns>
        [Route("{folder1}/{folder2}/{folder3}/{pageName}"), HttpGet]
        public Task<HttpResponseMessage> GetPage(string folder1, string folder2, string folder3, string pageName)
        {
            return GetPage($"{folder1}/{folder2}/{folder3}/{pageName}");
        }

        /// <summary>
        /// Sub-folder post handler - depth 3.
        /// </summary>
        /// <param name="folder1">First folder.</param>
        /// <param name="folder2">Second folder.</param>
        /// <param name="folder3">Third folder.</param>
        /// <param name="pageName">Page name to render.</param>
        /// <param name="postData">Post data.</param>
        /// <returns>Rendered page result for given page and path.</returns>
        [Route("{folder1}/{folder2}/{folder3}/{pageName}"), HttpPost]
        public Task<HttpResponseMessage> PostPage(string folder1, string folder2, string folder3, string pageName, dynamic postData)
        {
            return PostPage($"{folder1}/{folder2}/{folder3}/{pageName}", postData);
        }

        /// <summary>
        /// Sub-folder request handler - depth 4.
        /// </summary>
        /// <param name="folder1">First folder.</param>
        /// <param name="folder2">Second folder.</param>
        /// <param name="folder3">Third folder.</param>
        /// <param name="folder4">Fourth folder.</param>
        /// <param name="pageName">Page name to render.</param>
        /// <returns>Rendered page result for given page and path.</returns>
        [Route("{folder1}/{folder2}/{folder3}/{folder4}/{pageName}"), HttpGet]
        public Task<HttpResponseMessage> GetPage(string folder1, string folder2, string folder3, string folder4, string pageName)
        {
            return GetPage($"{folder1}/{folder2}/{folder3}/{folder4}/{pageName}");
        }

        /// <summary>
        /// Sub-folder post handler - depth 4.
        /// </summary>
        /// <param name="folder1">First folder.</param>
        /// <param name="folder2">Second folder.</param>
        /// <param name="folder3">Third folder.</param>
        /// <param name="folder4">Fourth folder.</param>
        /// <param name="pageName">Page name to render.</param>
        /// <param name="postData">Post data.</param>
        /// <returns>Rendered page result for given page and path.</returns>
        [Route("{folder1}/{folder2}/{folder3}/{folder4}/{pageName}"), HttpPost]
        public Task<HttpResponseMessage> PostPage(string folder1, string folder2, string folder3, string folder4, string pageName, dynamic postData)
        {
            return PostPage($"{folder1}/{folder2}/{folder3}/{folder4}/{pageName}", postData);
        }

        /// <summary>
        /// Sub-folder request handler - depth 5.
        /// </summary>
        /// <param name="folder1">First folder.</param>
        /// <param name="folder2">Second folder.</param>
        /// <param name="folder3">Third folder.</param>
        /// <param name="folder4">Fourth folder.</param>
        /// <param name="folder5">Fifth folder.</param>
        /// <param name="pageName">Page name to render.</param>
        /// <returns>Rendered page result for given page and path.</returns>
        [Route("{folder1}/{folder2}/{folder3}/{folder4}/{folder5}/{pageName}"), HttpGet]
        public Task<HttpResponseMessage> GetPage(string folder1, string folder2, string folder3, string folder4, string folder5, string pageName)
        {
            return GetPage($"{folder1}/{folder2}/{folder3}/{folder4}/{folder5}/{pageName}");
        }

        /// <summary>
        /// Sub-folder post handler - depth 5.
        /// </summary>
        /// <param name="folder1">First folder.</param>
        /// <param name="folder2">Second folder.</param>
        /// <param name="folder3">Third folder.</param>
        /// <param name="folder4">Fourth folder.</param>
        /// <param name="folder5">Fifth folder.</param>
        /// <param name="pageName">Page name to render.</param>
        /// <param name="postData">Post data.</param>
        /// <returns>Rendered page result for given page and path.</returns>
        [Route("{folder1}/{folder2}/{folder3}/{folder4}/{folder5}/{pageName}"), HttpPost]
        public Task<HttpResponseMessage> PostPage(string folder1, string folder2, string folder3, string folder4, string folder5, string pageName, dynamic postData)
        {
            return PostPage($"{folder1}/{folder2}/{folder3}/{folder4}/{folder5}/{pageName}", postData);
        }

        #endregion

        #endregion
    }
}