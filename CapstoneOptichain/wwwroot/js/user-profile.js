// Shared User Profile Management System
// This file contains functions for managing user profile data across all pages

// Load user profile data
async function loadUserProfile() {
    try {
        // Try to get data from server - try different controllers based on current page
        const currentPath = window.location.pathname;
        let controllerPath = '/Dashboard/GetUserProfileData'; // default
        
        if (currentPath.includes('/Admin/')) {
            controllerPath = '/Admin/GetUserProfileData';
        } else if (currentPath.includes('/Dashboardworker/')) {
            controllerPath = '/Dashboardworker/GetUserProfileData';
        } else if (currentPath.includes('/SupplierDashboard/')) {
            controllerPath = '/SupplierDashboard/GetUserProfileData';
        } else if (currentPath.includes('/Inventory/')) {
            controllerPath = '/Inventory/GetUserProfileData';
        } else if (currentPath.includes('/Inventoryworker/')) {
            controllerPath = '/Inventoryworker/GetUserProfileData';
        } else if (currentPath.includes('/Order/')) {
            controllerPath = '/Order/GetUserProfileData';
        } else if (currentPath.includes('/Orderworker/')) {
            controllerPath = '/Orderworker/GetUserProfileData';
        } else if (currentPath.includes('/Category/')) {
            controllerPath = '/Category/GetUserProfileData';
        }
        
        const response = await fetch(controllerPath);
        if (response.ok) {
            const data = await response.json();
            if (data.success) {
                // Update user name
                const userNameElement = document.getElementById('userName');
                const userFullNameElement = document.getElementById('userFullName');
                
                if (userNameElement) userNameElement.textContent = data.userName;
                if (userFullNameElement) userFullNameElement.textContent = data.userName;
                
                // Update profile image
                const profileImage = document.getElementById('userProfileImage');
                if (profileImage) {
                    const DEFAULT_DATA_URI = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMTIwIiBoZWlnaHQ9IjEyMCIgdmlld0JveD0iMCAwIDEyMCAxMjAiIGZpbGw9Im5vbmUiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+CjxyZWN0IHdpZHRoPSIxMjAiIGhlaWdodD0iMTIwIiByeD0iMjQiIGZpbGw9IiNFNUVGRkYiIHN0cm9rZT0iI0M3RURGRiIvPgo8Y2lyY2xlIGN4PSI2MCIgY3k9IjQ2IiByPSIyMiIgZmlsbD0iI0I2Q0RGRiIvPgo8cGF0aCBkPSJNMjQgMTAyQzI0IDg4IDM2IDc2IDUyIDc2SDY4Qzg0IDc2IDk2IDg4IDk2IDEwMkg2NEg1NkgyNCIgZmlsbD0iI0I2Q0RGRiIvPgo8L3N2Zz4=';
                    const serverUrl = data.profileImageUrl;
                    const resolved = !serverUrl || serverUrl === '/images/default-avatar.png' ? '/images/logo.png' : serverUrl;
                    profileImage.src = resolved;
                    profileImage.onerror = function() {
                        this.src = DEFAULT_DATA_URI;
                    };
                    
                    // Force image to show
                    profileImage.style.display = 'block';
                    profileImage.style.visibility = 'visible';
                    profileImage.style.opacity = '1';
                }
                
                // Store in localStorage for future use
                localStorage.setItem('userName', data.userName);
                localStorage.setItem('userImage', data.profileImageUrl || '/images/logo.png');
                
                return;
            }
        }
    } catch (error) {
        console.log('Could not fetch user profile from server, using cached data');
    }
    
    // Fallback to cached data if server request fails
    const userName = localStorage.getItem('userName') || sessionStorage.getItem('userName') || getCookie('userName') || 'My Profile';
    const userImage = localStorage.getItem('userImage') || sessionStorage.getItem('userImage') || '/images/default-avatar.png';
    
    // If we have a real user name, use it; otherwise keep "My Profile"
    if (userName && userName !== 'My Profile') {
        const userNameElement = document.getElementById('userName');
        const userFullNameElement = document.getElementById('userFullName');
        
        if (userNameElement) userNameElement.textContent = userName;
        if (userFullNameElement) userFullNameElement.textContent = userName;
    } else {
        // Try to get user name from server-side data
        const serverUserName = document.querySelector('meta[name="user-name"]')?.content || '';
        if (serverUserName && serverUserName !== '') {
            const userNameElement = document.getElementById('userName');
            const userFullNameElement = document.getElementById('userFullName');
            
            if (userNameElement) userNameElement.textContent = serverUserName;
            if (userFullNameElement) userFullNameElement.textContent = serverUserName;
        }
    }
    
    // Set profile image
    const profileImage = document.getElementById('userProfileImage');
    if (profileImage) {
        profileImage.src = userImage;
        profileImage.onerror = function() {
            this.src = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMTIwIiBoZWlnaHQ9IjEyMCIgdmlld0JveD0iMCAwIDEyMCAxMjAiIGZpbGw9Im5vbmUiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+CjxyZWN0IHdpZHRoPSIxMjAiIGhlaWdodD0iMTIwIiBmaWxsPSIjRjVGNUY1Ii8+CjxjaXJjbGUgY3g9IjYwIiBjeT0iNDAiIHI9IjIwIiBmaWxsPSIjQ0NDIi8+CjxwYXRoIGQ9Ik0yMCAxMDBDMTUgOTAgMjAgODAgMzAgNzVDNDAgNzAgNjAgNzAgODAgNzBDMTAwIDcwIDEyMCA3MCAxMzAgNzVDMTQwIDgwIDE0NSA5MCAxNDAgMTAwSDEyMFYxMDBIMjBaIiBmaWxsPSIjQ0NDIi8+Cjwvc3ZnPgo=';
        };
        
        // Force image to show even if src is the same
        profileImage.style.display = 'block';
        profileImage.style.visibility = 'visible';
    }
}

// Helper function to get cookie value
function getCookie(name) {
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);
    if (parts.length === 2) return parts.pop().split(';').shift();
    return null;
}

// Function to update user name (can be called from other scripts)
function updateUserName(newName) {
    if (newName && newName !== 'My Profile') {
        localStorage.setItem('userName', newName);
        sessionStorage.setItem('userName', newName);
        
        const userNameElement = document.getElementById('userName');
        const userFullNameElement = document.getElementById('userFullName');
        
        if (userNameElement) userNameElement.textContent = newName;
        if (userFullNameElement) userFullNameElement.textContent = newName;
        
        console.log('User name updated to:', newName);
    }
}

// Function to update user image
function updateUserImage(imageUrl) {
    if (imageUrl) {
        localStorage.setItem('userImage', imageUrl);
        sessionStorage.setItem('userImage', imageUrl);
        
        const profileImage = document.getElementById('userProfileImage');
        if (profileImage) {
            profileImage.src = imageUrl;
        }
        
        console.log('User image updated to:', imageUrl);
    }
}

// Function to get current user name
function getCurrentUserName() {
    return localStorage.getItem('userName') || sessionStorage.getItem('userName') || getCookie('userName') || 'My Profile';
}

// Function to handle Google sign up success
function handleGoogleSignUpSuccess(userData) {
    if (userData && userData.name) {
        // Save user name
        updateUserName(userData.name);
        
        // Save user image if available
        if (userData.picture) {
            updateUserImage(userData.picture);
        }
        
        console.log('Google sign up successful:', userData);
        
        // Redirect to dashboard or show success message
        setTimeout(function() {
            window.location.href = '/Dashboard/Index3';
        }, 1000);
    }
}

// Function to handle profile image errors
function handleProfileImageError(img) {
    img.onerror = function() {
        this.src = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMTIwIiBoZWlnaHQ9IjEyMCIgdmlld0JveD0iMCAwIDEyMCAxMjAiIGZpbGw9Im5vbmUiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+CjxyZWN0IHdpZHRoPSIxMjAiIGhlaWdodD0iMTIwIiBmaWxsPSIjRjVGNUY1Ii8+CjxjaXJjbGUgY3g9IjYwIiBjeT0iNDAiIHI9IjIwIiBmaWxsPSIjQ0NDIi8+CjxwYXRoIGQ9Ik0yMCAxMDBDMTUgOTAgMjAgODAgMzAgNzVDNDAgNzAgNjAgNzAgODAgNzBDMTAwIDcwIDEyMCA3MCAxMzAgNzVDMTQwIDgwIDE0NSA5MCAxNDAgMTAwSDEyMFYxMDBIMjBaIiBmaWxsPSIjQ0NDIi8+Cjwvc3ZnPgo=';
    };
}

// Function to remove Contact us from all pages
function removeContactUs() {
    let removedCount = 0;
    
    // Remove Contact us links from profile menu items
    const profileMenuItems = document.querySelectorAll('.profile-menu-item');
    profileMenuItems.forEach(item => {
        const text = item.textContent.trim().toLowerCase();
        if (text.includes('contact us') || text.includes('contact')) {
            item.style.display = 'none';
            item.remove();
            removedCount++;
        }
    });
    
    // Remove any links containing contact us
    const allLinks = document.querySelectorAll('a');
    allLinks.forEach(link => {
        const text = link.textContent.trim().toLowerCase();
        if (text.includes('contact us') || text.includes('contact')) {
            link.style.display = 'none';
            link.remove();
            removedCount++;
        }
    });
    
    // Remove any list items containing contact us
    const listItems = document.querySelectorAll('li');
    listItems.forEach(item => {
        const text = item.textContent.trim().toLowerCase();
        if (text.includes('contact us') || text.includes('contact')) {
            item.style.display = 'none';
            item.remove();
            removedCount++;
        }
    });
    
    // Remove any divs or spans containing contact us
    const allElements = document.querySelectorAll('div, span, p');
    allElements.forEach(element => {
        const text = element.textContent.trim().toLowerCase();
        if (text.includes('contact us') || text.includes('contact')) {
            // Only remove if it's not a parent element with other content
            if (element.children.length === 0 || element.textContent.trim() === 'Contact us') {
                element.style.display = 'none';
                element.remove();
                removedCount++;
            }
        }
    });
    
    // Also check for any remaining contact us text and hide it
    setTimeout(() => {
        const remainingElements = document.querySelectorAll('*');
        remainingElements.forEach(element => {
            if (element.textContent && element.textContent.trim().toLowerCase().includes('contact us')) {
                element.style.display = 'none';
                removedCount++;
            }
        });
    }, 100);
    
    if (removedCount > 0) {
        console.log(`Removed ${removedCount} Contact us elements`);
    }
}

// Initialize user profile when page loads
async function initializeUserProfile() {
    // Inject unified sidebar styles across all pages
    try {
        const STYLE_ID = 'global-sidebar-style';
        if (!document.getElementById(STYLE_ID)) {
            const style = document.createElement('style');
            style.id = STYLE_ID;
            style.textContent = `
                /* Global unified sidebar styles */
                body, .sidebar { font-family: Inter, system-ui, -apple-system, Segoe UI, Roboto, Ubuntu, Cantarell, Noto Sans, Arial, sans-serif !important; }
                .sidebar { width: 220px !important; background: #fff !important; border-right: 1px solid #ddd !important; padding: 20px !important; position: sticky !important; top: 0 !important; height: 100vh !important; overflow-y: auto !important; }
                .sidebar .logo { font-size: 24px !important; font-weight: 700 !important; margin-bottom: 30px !important; }
                .sidebar .menu a, .sidebar .bottom-menu a { display: flex !important; align-items: center !important; gap: 10px !important; padding: 10px !important; margin: 5px 0 !important; text-decoration: none !important; color: #333 !important; border-radius: 6px !important; font-size: 14px !important; line-height: 1.2 !important; }
                .sidebar .menu a i, .sidebar .bottom-menu a i { margin-right: 0 !important; font-size: 16px !important; min-width: 16px !important; text-align: center !important; }
                .sidebar .menu a.active, .sidebar .menu a:hover, .sidebar .bottom-menu a:hover { background: #eef3ff !important; color: #007bff !important; }
                /* Ensure notifications bell stays above */
                #notificationBell { position: fixed !important; top: 24px !important; right: 32px !important; z-index: 9999 !important; }
                /* Dark mode parity */
                body.dark-mode .sidebar { background: #1e1e1e !important; border-color: #333 !important; }
                body.dark-mode .sidebar .menu a, body.dark-mode .sidebar .bottom-menu a { color: #ccc !important; }
                body.dark-mode .sidebar .menu a.active, body.dark-mode .sidebar .menu a:hover, body.dark-mode .sidebar .bottom-menu a:hover { background: #333 !important; color: #fff !important; }
            `;
            document.head.appendChild(style);
        }
    } catch {}

    await loadUserProfile();
    
    // Apply error handling to all profile images
    const profileImages = document.querySelectorAll('#userProfileImage, #previewImg');
    profileImages.forEach(handleProfileImageError);
    
    // Remove Contact us from all pages
    removeContactUs();
    
    // Force profile image to be visible
    const profileImage = document.getElementById('userProfileImage');
    if (profileImage) {
        profileImage.style.display = 'block';
        profileImage.style.visibility = 'visible';
        profileImage.style.opacity = '1';
    }
    
    // Check for user name updates every few seconds
    setInterval(function() {
        const userNameElement = document.getElementById('userName');
        if (userNameElement) {
            const currentName = userNameElement.textContent;
            const storedName = localStorage.getItem('userName') || sessionStorage.getItem('userName') || getCookie('userName');
            
            if (storedName && storedName !== currentName && storedName !== 'My Profile') {
                const userFullNameElement = document.getElementById('userFullName');
                userNameElement.textContent = storedName;
                if (userFullNameElement) userFullNameElement.textContent = storedName;
            }
        }
    }, 2000);
    
    // Continuously remove Contact us to ensure it's always hidden
    setInterval(function() {
        removeContactUs();
    }, 1000);
}

// Make functions globally available
window.updateUserName = updateUserName;
window.updateUserImage = updateUserImage;
window.getCurrentUserName = getCurrentUserName;
window.handleGoogleSignUpSuccess = handleGoogleSignUpSuccess;
window.loadUserProfile = loadUserProfile;
window.initializeUserProfile = initializeUserProfile;
window.removeContactUs = removeContactUs;

// Auto-initialize when DOM is loaded
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => initializeUserProfile());
} else {
    initializeUserProfile();
}

// Also run when page is fully loaded
window.addEventListener('load', () => {
    removeContactUs();
});

// Run periodically to catch any dynamically added contact us elements
setInterval(() => {
    removeContactUs();
}, 500);

// Also run when DOM changes (for dynamically added content)
const observer = new MutationObserver(() => {
    removeContactUs();
});

// Start observing
observer.observe(document.body, {
    childList: true,
    subtree: true
});
