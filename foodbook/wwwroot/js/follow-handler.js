// Universal Follow/Unfollow Handler
// This script handles follow/unfollow functionality across all pages

(function() {
    'use strict';

    // Handle follow button clicks using event delegation
    document.addEventListener('click', async function(e) {
        const followBtn = e.target.closest('.follow-btn, .unfollow-btn');
        if (!followBtn) return;

        e.preventDefault();
        e.stopPropagation();

        const userId = parseInt(followBtn.getAttribute('data-user-id'));
        if (!userId || isNaN(userId)) {
            console.error('Invalid user ID:', followBtn.getAttribute('data-user-id'));
            return;
        }

        // Disable button during request
        const originalText = followBtn.textContent;
        const originalHtml = followBtn.innerHTML;
        followBtn.disabled = true;
        followBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Đang xử lý...';

        try {
            const response = await fetch('/Profile/ToggleFollow', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ userId: userId })
            });

            const data = await response.json();

            if (data.success) {
                // Check if this is a Newsfeed button (has follow-btn-sm class or rounded-pill class)
                const isNewsfeedButton = followBtn.classList.contains('follow-btn-sm') || followBtn.classList.contains('rounded-pill');
                
                // Check if this is a Recipe Detail button (check if current text contains "Theo dõi" or "Đã theo dõi")
                const currentText = followBtn.textContent.trim();
                const isRecipeDetailButton = currentText.includes('Theo dõi') || currentText.includes('theo dõi');
                
                // Update button state
                if (data.isFollowing) {
                    // Now following
                    followBtn.classList.remove('btn-outline-success', 'btn-success', 'btn-outline-primary', 'profile-edit-btn');
                    followBtn.classList.add('btn-outline-secondary');
                    // Use different text/icon based on page type
                    if (isRecipeDetailButton) {
                        followBtn.innerHTML = 'Đã theo dõi';
                    } else if (isNewsfeedButton) {
                        followBtn.innerHTML = '<i class="bi bi-person-plus"></i> Đã follow';
                    } else {
                        followBtn.innerHTML = '<i class="bi bi-person-check"></i> Đã follow';
                    }
                    followBtn.classList.remove('follow-btn');
                    followBtn.classList.add('unfollow-btn');
                    // Update data-is-following attribute if it exists
                    followBtn.setAttribute('data-is-following', 'true');
                } else {
                    // Now not following
                    followBtn.classList.remove('btn-outline-secondary');
                    // Use btn-success for Newsfeed, btn-outline-success for other pages
                    if (isNewsfeedButton) {
                        followBtn.classList.add('btn-success');
                    } else {
                        followBtn.classList.add('btn-outline-success');
                    }
                    // Use different text/icon based on page type
                    if (isRecipeDetailButton) {
                        followBtn.innerHTML = 'Theo dõi';
                    } else {
                        followBtn.innerHTML = '<i class="bi bi-person-plus"></i> Follow';
                    }
                    followBtn.classList.remove('unfollow-btn');
                    followBtn.classList.add('follow-btn');
                    // Update data-is-following attribute if it exists
                    followBtn.setAttribute('data-is-following', 'false');
                }
                followBtn.disabled = false;

                // Show success message
                showNotification(data.isFollowing ? 'Đã follow!' : 'Đã unfollow!', 'success');
            } else {
                // Show error message
                showNotification(data.message || 'Có lỗi xảy ra', 'error');
                followBtn.disabled = false;
                followBtn.innerHTML = originalHtml;
            }
        } catch (error) {
            console.error('Error toggling follow:', error);
            showNotification('Có lỗi xảy ra khi thực hiện thao tác', 'error');
            followBtn.disabled = false;
            followBtn.innerHTML = originalHtml;
        }
    });

    // Show notification function
    function showNotification(message, type = 'success') {
        // Remove existing notifications
        const existing = document.querySelector('.follow-notification');
        if (existing) existing.remove();

        const notification = document.createElement('div');
        notification.className = `follow-notification alert alert-${type === 'success' ? 'success' : 'danger'} alert-dismissible fade show`;
        notification.style.cssText = 'position: fixed; top: 20px; right: 20px; z-index: 9999; min-width: 250px;';
        notification.innerHTML = `
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        `;
        document.body.appendChild(notification);

        // Auto remove after 3 seconds
        setTimeout(() => {
            if (notification.parentNode) {
                notification.remove();
            }
        }, 3000);
    }
})();

