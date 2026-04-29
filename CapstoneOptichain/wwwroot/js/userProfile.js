// User Profile Management Script
// Load user profile data and update UI elements

async function loadUserProfile() {
    try {
        const response = await fetch('/Settings/GetUserData');
        const result = await response.json();
        
        if (result.success && result.data) {
            const userData = result.data;
            
            // Update profile image
            const profileImages = document.querySelectorAll('#userProfileImage');
            profileImages.forEach(img => {
                img.src = userData.ProfileImageUrl || '/images/default-avatar.png';
            });
            
            // Update user name
            const userNameElements = document.querySelectorAll('#userName, #userFullName');
            userNameElements.forEach(element => {
                element.textContent = userData.name || 'User';
            });
        }
    } catch (error) {
        console.error('Error loading user profile:', error);
    }
}

// Auto-load profile when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    loadUserProfile();
});
