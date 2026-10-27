// Bind existing jQuery requests to the garage that rendered this page. Never send
// context to another origin, and preserve all existing payloads/antiforgery headers.
(function () {
    var meta = document.querySelector('meta[name="garage-context"]');
    var antiforgery = document.querySelector('meta[name="request-verification-token"]');
    function unsafe(method) { return !/^(GET|HEAD|OPTIONS|TRACE)$/i.test(method || 'GET'); }
    if ((meta || antiforgery) && window.jQuery) {
        $.ajaxPrefilter(function (options, original, xhr) {
            var url = new URL(options.url, window.location.href);
            if (url.origin === window.location.origin) {
                if (meta) xhr.setRequestHeader('X-Garage-Context', meta.content);
                if (antiforgery && unsafe(options.type || options.method))
                    xhr.setRequestHeader('RequestVerificationToken', antiforgery.content);
            }
        });
    }
    if ((meta || antiforgery) && window.fetch) {
        var originalFetch = window.fetch.bind(window);
        window.fetch = function (input, init) {
            var request = new Request(input, init);
            if (new URL(request.url).origin === window.location.origin) {
                var headers = new Headers(request.headers);
                if (meta) headers.set('X-Garage-Context', meta.content);
                if (antiforgery && unsafe(request.method) && !headers.has('RequestVerificationToken'))
                    headers.set('RequestVerificationToken', antiforgery.content);
                request = new Request(request, { headers: headers });
            }
            return originalFetch(request);
        };
    }
    if (meta) window.addEventListener('pageshow', function (event) {
        if (event.persisted) window.location.reload();
    });
}());
