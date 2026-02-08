/**
 * Toast Notifications for WowClassicGrindBot
 * Lightweight notification system with auto-dismiss
 */

window.ToastNotifications = (function () {
    'use strict';

    let container = null;

    // Ensure container exists
    function ensureContainer() {
        if (!container) {
            container = document.createElement('div');
            container.className = 'ds-toast-container';
            container.setAttribute('role', 'alert');
            container.setAttribute('aria-live', 'polite');
            document.body.appendChild(container);
        }
        return container;
    }

    // Icon SVGs for different toast types
    const icons = {
        success: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#22c55e" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>',
        error: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#dc3545" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>',
        warning: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#ffc107" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>',
        info: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#0dcaf0" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>'
    };

    /**
     * Show a toast notification
     * @param {Object} options - Toast configuration
     * @param {string} options.title - Toast title
     * @param {string} options.message - Toast message (optional)
     * @param {string} options.type - Toast type: 'success' | 'error' | 'warning' | 'info'
     * @param {number} options.duration - Auto-dismiss duration in ms (default: 5000, 0 = no auto-dismiss)
     */
    function show(options) {
        const {
            title = 'Notification',
            message = '',
            type = 'info',
            duration = 5000
        } = options;

        const container = ensureContainer();

        // Create toast element
        const toast = document.createElement('div');
        toast.className = 'ds-toast';
        toast.innerHTML = `
            <div class="ds-toast-icon">${icons[type] || icons.info}</div>
            <div class="ds-toast-content">
                <div class="ds-toast-title">${escapeHtml(title)}</div>
                ${message ? `<div class="ds-toast-message">${escapeHtml(message)}</div>` : ''}
            </div>
            <button class="ds-toast-close" aria-label="Close notification">&times;</button>
        `;

        // Add close functionality
        const closeBtn = toast.querySelector('.ds-toast-close');
        closeBtn.addEventListener('click', () => dismiss(toast));

        // Add to container
        container.appendChild(toast);

        // Auto-dismiss
        if (duration > 0) {
            setTimeout(() => dismiss(toast), duration);
        }

        return toast;
    }

    /**
     * Dismiss a toast with animation
     * @param {HTMLElement} toast - Toast element to dismiss
     */
    function dismiss(toast) {
        if (!toast || toast.dataset.dismissing) return;
        toast.dataset.dismissing = 'true';

        toast.style.animation = 'ds-toast-out 0.3s ease-in forwards';
        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 300);
    }

    /**
     * Clear all toasts
     */
    function clearAll() {
        const container = ensureContainer();
        const toasts = container.querySelectorAll('.ds-toast');
        toasts.forEach(toast => dismiss(toast));
    }

    // Helper to escape HTML
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Add toast-out animation to document
    const style = document.createElement('style');
    style.textContent = `
        @keyframes ds-toast-out {
            from { opacity: 1; transform: translateX(0); }
            to { opacity: 0; transform: translateX(100%); }
        }
    `;
    document.head.appendChild(style);

    // Convenience methods
    return {
        show,
        dismiss,
        clearAll,
        success: (title, message, duration) => show({ title, message, type: 'success', duration }),
        error: (title, message, duration) => show({ title, message, type: 'error', duration }),
        warning: (title, message, duration) => show({ title, message, type: 'warning', duration }),
        info: (title, message, duration) => show({ title, message, type: 'info', duration })
    };
})();
