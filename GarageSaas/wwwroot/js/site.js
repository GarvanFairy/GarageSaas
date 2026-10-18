// Bind existing jQuery requests to the garage that rendered this page. Never send
// context to another origin, and preserve all existing payloads/antiforgery headers.
(function () {
    var meta = document.querySelector('meta[name="garage-context"]');
    if (meta && window.jQuery) {
        $.ajaxPrefilter(function (options, original, xhr) {
            var url = new URL(options.url, window.location.href);
            if (url.origin === window.location.origin) {
                xhr.setRequestHeader('X-Garage-Context', meta.content);
            }
        });
    }
    if (meta && window.fetch) {
        var originalFetch = window.fetch.bind(window);
        window.fetch = function (input, init) {
            var request = new Request(input, init);
            if (new URL(request.url).origin === window.location.origin) {
                var headers = new Headers(request.headers);
                headers.set('X-Garage-Context', meta.content);
                request = new Request(request, { headers: headers });
            }
            return originalFetch(request);
        };
    }
    if (meta) window.addEventListener('pageshow', function (event) {
        if (event.persisted) window.location.reload();
    });
}());
