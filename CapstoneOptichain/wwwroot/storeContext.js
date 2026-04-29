// Ensure store is selected before accessing inventory
document.addEventListener('DOMContentLoaded', async function () {
    const currentPath = window.location.pathname;

    // Skip check if we're on admin pages, dashboard, or store selection page
    if (currentPath === '/Order/Index2' || 
        currentPath.startsWith('/Admin/') || 
        currentPath.startsWith('/Dashboard/')) return;

    try {
        const response = await fetch('/Order/CheckStoreSelection');
        const data = await response.json();

        if (!data.hasStore) {
            // Check localStorage as fallback
            const localStoreId = localStorage.getItem('selectedStoreId');
            const localStoreName = localStorage.getItem('selectedStoreName');

            if (localStoreId && localStoreName) {
                // Try to sync with server
                const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
                if (tokenElement) {
                    const token = tokenElement.value;
                    const syncResponse = await fetch('/Order/SetCurrentStore', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': token
                        },
                        body: JSON.stringify({
                            storeId: localStoreId,
                            storeName: localStoreName
                        })
                    });

                    if (syncResponse.ok) {
                        return;
                    }
                }
            }

            // Redirect to store selection if no store is selected
            window.location.href = '/Order/Index2';
        } else {
            // Update localStorage with server data
            localStorage.setItem('selectedStoreId', data.storeId);
            localStorage.setItem('selectedStoreName', data.storeName);

            // Update UI if current store info element exists
            const currentStoreInfoElement = document.getElementById('currentStoreInfo');
            if (currentStoreInfoElement) {
                currentStoreInfoElement.textContent = `Current Store: ${data.storeName}`;
            }
        }
    } catch (error) {
        console.error('Error checking store selection:', error);
    }
});

// Modify fetch requests to include storeId
const originalFetch = window.fetch;
window.fetch = async function (url, options = {}) {
    // Only modify requests to our own endpoints
    if (typeof url === 'string' &&
        (url.startsWith('/Inventory/') || url.startsWith('/Order/'))) {

        // Ensure we have a store selected
        const storeId = localStorage.getItem('selectedStoreId');
        if (!storeId) {
            // Only redirect if we're not already on the store selection page
            if (window.location.pathname !== '/Order/Index2') {
                window.location.href = '/Order/Index2';
            }
            return originalFetch.call(this, url, options);
        }

        // For GET requests, add storeId as query parameter
        if (!options.method || options.method === 'GET') {
            const separator = url.includes('?') ? '&' : '?';
            url = `${url}${separator}storeId=${storeId}`;
        }
        // For other methods, add storeId to body
        else if (options.body) {
            if (typeof options.body === 'string') {
                try {
                    const bodyObj = JSON.parse(options.body);
                    bodyObj.storeId = storeId;
                    options.body = JSON.stringify(bodyObj);
                } catch {
                    // If body is not JSON, leave it unchanged
                }
            } else if (typeof options.body === 'object') {
                options.body.storeId = storeId;
            }
        }
    }

    return originalFetch.call(this, url, options);
};

// Function to get current store ID
function getCurrentStoreId() {
    return localStorage.getItem('selectedStoreId');
}