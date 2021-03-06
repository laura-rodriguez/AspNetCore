using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Http.Internal
{
    public sealed class ReusableHttpResponse : HttpResponse
    {
        // Lambdas hoisted to static readonly fields to improve inlining https://github.com/dotnet/roslyn/issues/13624
        private readonly static Func<IFeatureCollection, IHttpResponseFeature> _nullResponseFeature = f => null;
        private readonly static Func<IFeatureCollection, IResponseCookiesFeature> _newResponseCookiesFeature = f => new ResponseCookiesFeature(f);
        private readonly static Func<HttpContext, IResponseBodyPipeFeature> _newResponseBodyPipeFeature = context => new ResponseBodyPipeFeature(context);

        private HttpContext _context;
        private FeatureReferences<FeatureInterfaces> _features;

        public ReusableHttpResponse(HttpContext context)
        {
            Initialize(context);
        }

        public void Initialize(HttpContext context)
        {
            _context = context;
            _features = new FeatureReferences<FeatureInterfaces>(context.Features);
        }

        public void Uninitialize()
        {
            _context = null;
            _features = default;
        }

        private IHttpResponseFeature HttpResponseFeature =>
            _features.Fetch(ref _features.Cache.Response, _nullResponseFeature);

        private IResponseCookiesFeature ResponseCookiesFeature =>
            _features.Fetch(ref _features.Cache.Cookies, _newResponseCookiesFeature);
        private IResponseBodyPipeFeature ResponseBodyPipeFeature =>
            _features.Fetch(ref _features.Cache.BodyPipe, this.HttpContext, _newResponseBodyPipeFeature);

        public override HttpContext HttpContext { get { return _context; } }

        public override int StatusCode
        {
            get { return HttpResponseFeature.StatusCode; }
            set { HttpResponseFeature.StatusCode = value; }
        }

        public override IHeaderDictionary Headers
        {
            get { return HttpResponseFeature.Headers; }
        }

        public override Stream Body
        {
            get { return HttpResponseFeature.Body; }
            set { HttpResponseFeature.Body = value; }
        }

        public override long? ContentLength
        {
            get { return Headers.ContentLength; }
            set { Headers.ContentLength = value; }
        }

        public override string ContentType
        {
            get
            {
                return Headers[HeaderNames.ContentType];
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    HttpResponseFeature.Headers.Remove(HeaderNames.ContentType);
                }
                else
                {
                    HttpResponseFeature.Headers[HeaderNames.ContentType] = value;
                }
            }
        }

        public override IResponseCookies Cookies
        {
            get { return ResponseCookiesFeature.Cookies; }
        }

        public override PipeWriter BodyPipe
        {
            get { return ResponseBodyPipeFeature.ResponseBodyPipe; }
            set { ResponseBodyPipeFeature.ResponseBodyPipe = value; }
        }

        public override bool HasStarted
        {
            get { return HttpResponseFeature.HasStarted; }
        }

        public override void OnStarting(Func<object, Task> callback, object state)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            HttpResponseFeature.OnStarting(callback, state);
        }

        public override void OnCompleted(Func<object, Task> callback, object state)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            HttpResponseFeature.OnCompleted(callback, state);
        }

        public override void Redirect(string location, bool permanent)
        {
            if (permanent)
            {
                HttpResponseFeature.StatusCode = 301;
            }
            else
            {
                HttpResponseFeature.StatusCode = 302;
            }

            Headers[HeaderNames.Location] = location;
        }

        struct FeatureInterfaces
        {
            public IHttpResponseFeature Response;
            public IResponseCookiesFeature Cookies;
            public IResponseBodyPipeFeature BodyPipe;
        }
    }
}
