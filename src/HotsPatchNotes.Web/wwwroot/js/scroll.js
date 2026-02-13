// Smooth scroll to anchor
window.scrollToAnchor = (anchorId) => {
    const element = document.getElementById(anchorId);
    if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'start' });
        return true;
    }
    return false;
};

// Intercept anchor link clicks for smooth scrolling
window.setupAnchorInterception = () => {
    document.addEventListener('click', (e) => {
        const target = e.target.closest('a');
        if (target && target.href) {
            const url = new URL(target.href);
            // Only intercept if it's a hash link on the same page
            if (url.hash && url.pathname === window.location.pathname) {
                e.preventDefault();
                const anchorId = url.hash.substring(1);
                window.scrollToAnchor(anchorId);
                // Update URL hash without triggering scroll
                history.pushState(null, null, url.hash);
            }
        }
    });
};

// Call on page load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', window.setupAnchorInterception);
} else {
    window.setupAnchorInterception();
}
