// Smooth scroll to anchor
window.scrollToAnchor = (anchorId) => {
    const element = document.getElementById(anchorId);
    if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'start' });
        return true;
    }
    console.warn(`Anchor element not found: ${anchorId}`);
    return false;
};

// Convenience function to scroll to top
window.scrollToTop = () => {
    return window.scrollToAnchor('return');
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

    // Handle initial page load with hash
    if (window.location.hash) {
        const anchorId = window.location.hash.substring(1);
        // Small delay to ensure page is fully loaded
        setTimeout(() => {
            window.scrollToAnchor(anchorId);
        }, 100);
    }
};

// Call on page load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', window.setupAnchorInterception);
} else {
    window.setupAnchorInterception();
}
