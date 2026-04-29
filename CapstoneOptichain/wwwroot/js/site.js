// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
    function setDirAndLang(culture) {
        var isArabic = culture && culture.toLowerCase().startsWith('ar');
        document.documentElement.setAttribute('lang', isArabic ? 'ar' : 'en');
        document.documentElement.setAttribute('dir', isArabic ? 'rtl' : 'ltr');
        document.body.classList.toggle('rtl', isArabic);
    }

    function applyRtlStylesIfNeeded() {
        if (document.getElementById('rtl-style-injected')) return;
        var style = document.createElement('style');
        style.id = 'rtl-style-injected';
        style.innerHTML = "\n        body.rtl { direction: rtl; }\n        body.rtl .sidebar .menu a i, body.rtl .sidebar .bottom-menu a i { margin-left: 10px; margin-right: 0; }\n        body.rtl .sidebar .menu a, body.rtl .sidebar .bottom-menu a { text-align: right; }\n        body.rtl .products-table th, body.rtl .products-table td { text-align: right; }\n        body.rtl .user-profile-trigger i { transform: scaleX(-1); }\n        body.rtl .header .search-and-toggle { flex-direction: row-reverse; }\n        body.rtl .notification-modal .notification-time { text-align: left; }\n        ";
        document.head.appendChild(style);
    }

    function setCulture(culture) {
        try {
            var body = 'culture=' + encodeURIComponent(culture) + '&returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search);
            fetch('/Localization/SetLanguage', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                credentials: 'same-origin',
                body: body
            }).then(function(){
                window.location.reload();
            }).catch(function(err){
                console.error('Failed to set culture', err);
                window.location.reload();
            });
        } catch (e) {
            console.error('Failed to set culture', e);
            window.location.reload();
        }
    }

    // Expose setter globally for UI elements in views
    try { window.setAppCulture = setCulture; } catch (_) {}

    function init() {
        fetch('/Localization/CurrentCulture', { credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                var culture = (data && data.culture) || 'en';
                setDirAndLang(culture);
                applyRtlStylesIfNeeded();
            })
            .catch(function () {
                // Fallback to EN if endpoint not available
                setDirAndLang('en');
                applyRtlStylesIfNeeded();
            });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
