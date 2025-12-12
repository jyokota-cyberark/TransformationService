/**
 * Pagination Helper - Reusable pagination functionality across all services
 * Handles page size selection, localStorage persistence, and URL parameter management
 */

class PaginationManager {
    constructor(paginationId = 'pagination-control', options = {}) {
        this.paginationId = paginationId;
        this.options = {
            storagePrefix: 'pagination_',
            onPageChange: options.onPageChange || null,
            onPageSizeChange: options.onPageSizeChange || null,
            ...options
        };
        
        this.pageSizeSelect = document.getElementById(`${paginationId}-pageSize`);
        this.pageLinks = document.querySelectorAll(`[data-pagination="${paginationId}"] [data-page]`);
        
        this.init();
    }

    init() {
        this.attachPageSizeListener();
        this.attachPageLinkListeners();
        this.restorePageSizePreference();
    }

    attachPageSizeListener() {
        if (!this.pageSizeSelect) return;

        this.pageSizeSelect.addEventListener('change', (e) => {
            const newPageSize = e.target.value;
            this.setPageSizePreference(newPageSize);
            
            if (this.options.onPageChange) {
                this.options.onPageChange(newPageSize);
            } else {
                this.navigateToPage(1, newPageSize);
            }
        });
    }

    attachPageLinkListeners() {
        this.pageLinks.forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                
                if (link.parentElement.classList.contains('disabled')) {
                    return;
                }

                const page = link.getAttribute('data-page');
                
                if (this.options.onPageChange) {
                    this.options.onPageChange(page);
                } else {
                    this.navigateToPage(page);
                }
            });
        });
    }

    /**
     * Store page size preference in localStorage
     */
    setPageSizePreference(pageSize) {
        const key = `${this.options.storagePrefix}${this.paginationId}-pageSize`;
        localStorage.setItem(key, pageSize);
    }

    /**
     * Restore page size preference from localStorage
     */
    restorePageSizePreference() {
        if (!this.pageSizeSelect) return;

        const key = `${this.options.storagePrefix}${this.paginationId}-pageSize`;
        const savedPageSize = localStorage.getItem(key);
        
        if (savedPageSize && savedPageSize !== this.pageSizeSelect.value) {
            this.pageSizeSelect.value = savedPageSize;
        }
    }

    /**
     * Navigate to a specific page with optional page size
     */
    navigateToPage(page, pageSize = null) {
        const url = new URL(window.location);
        url.searchParams.set('page', page);
        
        if (pageSize) {
            url.searchParams.set('pageSize', pageSize);
        }
        
        window.location = url.toString();
    }

    /**
     * Update pagination controls (useful for AJAX pagination)
     */
    updateControls(data) {
        // This method can be overridden to handle AJAX updates
        // data should contain: { currentPage, pageSize, totalPages, totalCount }
    }

    /**
     * Get current pagination state
     */
    getState() {
        const url = new URL(window.location);
        return {
            page: parseInt(url.searchParams.get('page')) || 1,
            pageSize: parseInt(url.searchParams.get('pageSize')) || 15,
        };
    }
}

/**
 * Initialize all pagination managers on the page
 */
function initPaginationManagers() {
    const paginationControls = document.querySelectorAll('[data-pagination]');
    paginationControls.forEach(control => {
        const paginationId = control.getAttribute('data-pagination');
        new PaginationManager(paginationId);
    });
}

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initPaginationManagers);
} else {
    initPaginationManagers();
}
