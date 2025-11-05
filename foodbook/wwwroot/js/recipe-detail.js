/**
 * Recipe Detail Page JavaScript
 * Handles media display, user interactions, and form submissions
 */

// Store current media index for each step
let currentMediaIndexes = {};
let galleryTimer = null;
let galleryIndex = 0;
let galleryThumbs = [];
const SLIDE_INTERVAL_MS = 5000; // auto-rotate interval
const SLIDE_DURATION_MS = 1000; // smoother slide transition duration

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    initializeMediaIndexes();
    fitMediaContainers();
    setupCollapseListeners();
    setupResizeHandler();
    initializeSaveButton();
    initializeLikeButton();
    initializeFollowButton();
    initializeShareButton();
    initializeCommentForm();
    initializeReportButton();
    initializeGallery();
    startGalleryAutoRotate();
});

// Initialize media indexes for each step
function initializeMediaIndexes() {
    document.querySelectorAll('.step-media-wrapper').forEach(container => {
        const stepMatch = container.id.match(/mediaContainer(\d+)/);
        if (stepMatch) {
            const stepNum = parseInt(stepMatch[1]);
            currentMediaIndexes[stepNum] = 0;
        }
    });
}

// Setup collapse event listeners
function setupCollapseListeners() {
    document.querySelectorAll('.collapse').forEach(collapse => {
        collapse.addEventListener('shown.bs.collapse', function() {
            setTimeout(function() {
                const mediaWrapper = collapse.querySelector('.step-media-wrapper');
                if (mediaWrapper) {
                    const visibleItems = mediaWrapper.querySelectorAll('.step-media-item');
                    visibleItems.forEach(container => {
                        if (container.style.display !== 'none') {
                            const media = container.querySelector('.step-image');
                            if (media) {
                                adjustContainerToMedia(container, media);
                            }
                        }
                    });
                }
            }, 100);
        });
    });
}

// Setup window resize handler
function setupResizeHandler() {
    let resizeTimeout;
    window.addEventListener('resize', function() {
        clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(function() {
            document.querySelectorAll('.step-media-item').forEach(container => {
                if (container.style.display !== 'none') {
                    const media = container.querySelector('.step-image');
                    if (media) {
                        adjustContainerToMedia(container, media);
                    }
                }
            });
        }, 250);
    });
}

// Fit all media containers to their content aspect ratio
function fitMediaContainers() {
    document.querySelectorAll('.step-media-item').forEach(container => {
        const media = container.querySelector('.step-image');
        if (!media) return;
        
        if (container.style.display === 'none') return;
        
        if (media.tagName === 'VIDEO') {
            fitVideoContainer(container, media);
        } else if (media.tagName === 'IMG') {
            fitImageContainer(container, media);
        }
    });
}

// Fit video container
function fitVideoContainer(container, media) {
    if (media.readyState < 1) {
        media.load();
    }
    
    let retryCount = 0;
    const maxRetries = 20;
    
    const tryAdjust = function() {
        if (media.videoWidth && media.videoHeight) {
            adjustContainerToMedia(container, media);
            return true;
        } else if (retryCount < maxRetries) {
            retryCount++;
            setTimeout(tryAdjust, 150);
            return false;
        }
        return false;
    };
    
    if (!tryAdjust()) {
        media.addEventListener('loadedmetadata', function() {
            adjustContainerToMedia(container, media);
        }, { once: true });
    }
}

// Fit image container
function fitImageContainer(container, media) {
    if (media.complete && media.naturalWidth) {
        adjustContainerToMedia(container, media);
    } else {
        media.addEventListener('load', function() {
            adjustContainerToMedia(container, media);
        });
    }
}

// Adjust container to match media aspect ratio
function adjustContainerToMedia(container, media) {
    const naturalWidth = media.videoWidth || media.naturalWidth;
    const naturalHeight = media.videoHeight || media.naturalHeight;
    
    if (!naturalWidth || !naturalHeight) {
        console.log('Media dimensions not available yet');
        return;
    }
    
    const aspectRatio = naturalWidth / naturalHeight;
    const wrapper = container.closest('.step-media-wrapper');
    if (!wrapper) return;
    
    const wrapperWidth = wrapper.offsetWidth;
    const calculatedHeight = wrapperWidth / aspectRatio;
    
    container.style.width = wrapperWidth + 'px';
    container.style.height = calculatedHeight + 'px';
    
    media.style.width = '100%';
    media.style.height = '100%';
    media.style.objectFit = 'contain';
    
    console.log(`Adjusted container: ${naturalWidth}x${naturalHeight} (${aspectRatio.toFixed(2)}), wrapper: ${wrapperWidth}px, calculated height: ${calculatedHeight}px`);
}

// Navigate step media (prev/next)
function navigateStepMedia(stepNumber, direction) {
    const container = document.getElementById('mediaContainer' + stepNumber);
    if (!container) return;
    
    const mediaItems = container.querySelectorAll('.step-media-item');
    if (mediaItems.length <= 1) return;
    
    mediaItems[currentMediaIndexes[stepNumber]].style.display = 'none';
    
    currentMediaIndexes[stepNumber] += direction;
    
    if (currentMediaIndexes[stepNumber] < 0) {
        currentMediaIndexes[stepNumber] = mediaItems.length - 1;
    } else if (currentMediaIndexes[stepNumber] >= mediaItems.length) {
        currentMediaIndexes[stepNumber] = 0;
    }
    
    const newMediaItem = mediaItems[currentMediaIndexes[stepNumber]];
    newMediaItem.style.display = 'inline-flex';
    
    const newMedia = newMediaItem.querySelector('.step-image');
    if (newMedia) {
        adjustContainerToMedia(newMediaItem, newMedia);
    }
}

// Get CSRF token
function getCSRFToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
}

// Initialize save button
function initializeSaveButton() {
    const btnSave = document.getElementById('btnSaveRecipe');
    if (!btnSave) return;
    
    btnSave.addEventListener('click', async function() {
        const recipeId = this.dataset.recipeId;
        const isSaved = this.dataset.isSaved === 'true';
        
        try {
            const formData = new FormData();
            formData.append('recipeId', recipeId);
            formData.append('isSaved', isSaved);
            formData.append('__RequestVerificationToken', getCSRFToken());

            const response = await fetch('/Recipe/ToggleSave', {
                method: 'POST',
                body: formData
            });
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    const icon = this.querySelector('i');
                    const buttonText = this.querySelector('.save-text');
                    
                    if (result.isSaved) {
                        icon.classList.remove('bi-bookmark');
                        icon.classList.add('bi-bookmark-fill');
                        this.classList.remove('btn-success');
                        this.classList.add('btn-warning');
                        if (buttonText) buttonText.textContent = 'Đã lưu';
                        showAlertModal('Đã lưu vào sổ tay', 'success');
                    } else {
                        icon.classList.remove('bi-bookmark-fill');
                        icon.classList.add('bi-bookmark');
                        this.classList.remove('btn-warning');
                        this.classList.add('btn-success');
                        if (buttonText) buttonText.textContent = 'Lưu công thức';
                        showAlertModal('Đã xóa khỏi sổ tay', 'success');
                    }
                    this.dataset.isSaved = result.isSaved;
                } else {
                    showAlertModal(result.message || 'Có lỗi xảy ra', 'error');
                }
            } else {
                showAlertModal('Không thể kết nối đến máy chủ', 'error');
            }
        } catch (error) {
            console.error('Error toggling save:', error);
            showAlertModal('Có lỗi xảy ra. Vui lòng thử lại.', 'error');
        }
    });
}

// Initialize gallery thumbnails
function initializeGallery() {
    const mainDisplay = document.getElementById('mainMediaDisplay');
    if (!mainDisplay) return;
    // ensure initial slide is created from background-image
    setupInitialMainSlide();
    galleryThumbs = Array.from(document.querySelectorAll('.gallery-thumb'));
    galleryThumbs.forEach((thumb, idx) => {
        thumb.addEventListener('click', function() {
            const mediaType = this.dataset.mediaType;
            const mediaUrl = this.dataset.mediaUrl;
            const direction = idx > galleryIndex ? 1 : -1;
            setMainMedia(mediaUrl, mediaType, direction);
            galleryIndex = idx;
            restartGalleryAutoRotate();
        });
    });
}

// Convert initial background-image into a slide layer
function setupInitialMainSlide() {
    const container = document.getElementById('mainMediaDisplay');
    if (!container) return;
    const bg = container.style.backgroundImage;
    const hasSlide = container.querySelector('.media-slide');
    if (hasSlide) return;
    const slide = document.createElement('div');
    slide.className = 'media-slide active';
    applySlideBaseStyles(slide);
    if (bg && bg !== 'none') {
        slide.style.backgroundImage = bg;
        slide.style.backgroundPosition = 'center';
        slide.style.backgroundRepeat = 'no-repeat';
        slide.style.backgroundSize = 'cover';
        container.style.backgroundImage = 'none';
    }
    container.appendChild(slide);
}

// Swap main media display
function setMainMedia(url, type, direction = 1) {
    const mainDisplay = document.getElementById('mainMediaDisplay');
    if (!mainDisplay) return;
    const current = mainDisplay.querySelector('.media-slide.active');
    const next = document.createElement('div');
    next.className = 'media-slide';
    applySlideBaseStyles(next);
    // prepare next content
    if (type === 'video') {
        mainDisplay.style.backgroundImage = 'none';
        const video = document.createElement('video');
        video.controls = true;
        video.preload = 'metadata';
        video.style.width = '100%';
        video.style.height = '100%';
        video.innerHTML = `<source src="${url}" type="video/mp4">`;
        next.appendChild(video);
    } else {
        next.style.backgroundImage = `url('${url}')`;
        next.style.backgroundPosition = 'center';
        next.style.backgroundRepeat = 'no-repeat';
        next.style.backgroundSize = 'cover';
    }

    // slide in from right/left
    next.style.transform = `translateX(${direction > 0 ? '100%' : '-100%'})`;
    mainDisplay.appendChild(next);
    // force reflow before starting transition
    void next.offsetWidth;
    if (current) {
        current.style.transition = `transform ${SLIDE_DURATION_MS}ms ease`;
        current.style.transform = `translateX(${direction > 0 ? '-100%' : '100%'})`;
        current.classList.remove('active');
    }
    next.style.transition = `transform ${SLIDE_DURATION_MS}ms ease`;
    next.style.transform = 'translateX(0)';
    next.classList.add('active');

    const cleanup = () => {
        if (current && current.parentElement === mainDisplay) {
            mainDisplay.removeChild(current);
        }
        next.removeEventListener('transitionend', cleanup);
    };
    next.addEventListener('transitionend', cleanup);
}

// Auto-rotate main gallery every 5 seconds
function startGalleryAutoRotate() {
    restartGalleryAutoRotate();
}

function restartGalleryAutoRotate() {
    if (galleryTimer) {
        clearInterval(galleryTimer);
        galleryTimer = null;
    }
    if (!galleryThumbs || galleryThumbs.length <= 1) return;
    galleryTimer = setInterval(() => {
        galleryIndex = (galleryIndex + 1) % galleryThumbs.length;
        const next = galleryThumbs[galleryIndex];
        if (!next) return;
        const mediaType = next.dataset.mediaType;
        const mediaUrl = next.dataset.mediaUrl;
        setMainMedia(mediaUrl, mediaType, 1);
    }, SLIDE_INTERVAL_MS);
}

function applySlideBaseStyles(el) {
    el.style.position = 'absolute';
    el.style.inset = '0';
    el.style.width = '100%';
    el.style.height = '100%';
}

// Initialize like button
function initializeLikeButton() {
    const btnLike = document.getElementById('btnLike');
    if (!btnLike) return;
    
    btnLike.addEventListener('click', async function() {
        const recipeId = this.dataset.recipeId;
        const isLiked = this.dataset.isLiked === 'true';
        
        try {
            const formData = new FormData();
            formData.append('recipeId', recipeId);
            formData.append('isLiked', isLiked);
            formData.append('__RequestVerificationToken', getCSRFToken());

            const response = await fetch('/Recipe/ToggleLike', {
                method: 'POST',
                body: formData
            });
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    const icon = this.querySelector('i');
                    const countSpan = this.querySelector('#likeCount');
                    
                    if (result.isLiked) {
                        icon.classList.remove('bi-hand-thumbs-up');
                        icon.classList.add('bi-hand-thumbs-up-fill', 'text-primary');
                    } else {
                        icon.classList.remove('bi-hand-thumbs-up-fill', 'text-primary');
                        icon.classList.add('bi-hand-thumbs-up');
                    }
                    
                    countSpan.textContent = result.likeCount;
                    this.dataset.isLiked = result.isLiked;
                }
            }
        } catch (error) {
            console.error('Error toggling like:', error);
        }
    });
}

// Initialize follow button
function initializeFollowButton() {
    const btnFollow = document.querySelector('.follow-btn');
    if (!btnFollow) return;
    
    btnFollow.addEventListener('click', async function() {
        const userId = this.dataset.userId;
        const isFollowing = this.dataset.isFollowing === 'true';
        
        try {
            const response = await fetch('/Home/ToggleFollow', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: `userId=${userId}&isFollowing=${isFollowing}&__RequestVerificationToken=${getCSRFToken()}`
            });
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    if (result.isFollowing) {
                        this.classList.remove('btn-outline-success');
                        this.classList.add('btn-outline-secondary');
                        this.textContent = 'Đã theo dõi';
                    } else {
                        this.classList.remove('btn-outline-secondary');
                        this.classList.add('btn-outline-success');
                        this.textContent = 'Theo dõi';
                    }
                    this.dataset.isFollowing = result.isFollowing;
                }
            }
        } catch (error) {
            console.error('Error toggling follow:', error);
        }
    });
}

// Initialize share button
function initializeShareButton() {
    const btnShare = document.getElementById('btnShare');
    if (!btnShare) return;
    
    btnShare.addEventListener('click', async function() {
        const recipeId = this.dataset.recipeId;
        const recipeUrl = window.location.href;
        const recipeTitle = document.querySelector('.recipe-title')?.textContent || 'Công thức nấu ăn';
        
        try {
            if (navigator.share) {
                await navigator.share({
                    title: recipeTitle,
                    text: `Xem công thức "${recipeTitle}" trên FoodBook`,
                    url: recipeUrl
                });
                await recordShare(recipeId);
            } else {
                await navigator.clipboard.writeText(recipeUrl);
                alert('Đã sao chép link công thức vào clipboard!');
                await recordShare(recipeId);
            }
        } catch (error) {
            console.error('Error sharing:', error);
            try {
                await navigator.clipboard.writeText(recipeUrl);
                alert('Đã sao chép link công thức vào clipboard!');
                await recordShare(recipeId);
            } catch (clipboardError) {
                console.error('Clipboard error:', clipboardError);
                alert('Không thể chia sẻ. Vui lòng thử lại.');
            }
        }
    });
}

// Record share in database
async function recordShare(recipeId) {
    try {
        const formData = new FormData();
        formData.append('recipeId', recipeId);
        formData.append('__RequestVerificationToken', getCSRFToken());

        const response = await fetch('/Recipe/RecordShare', {
            method: 'POST',
            body: formData
        });
        
        if (response.ok) {
            const result = await response.json();
            if (result.success) {
                const shareCountSpan = document.getElementById('shareCount');
                if (shareCountSpan) {
                    const currentCount = parseInt(shareCountSpan.textContent) || 0;
                    shareCountSpan.textContent = currentCount + 1;
                }
            }
        }
    } catch (error) {
        console.error('Error recording share:', error);
    }
}

// Initialize comment form
function initializeCommentForm() {
    const commentForm = document.getElementById('commentForm');
    if (!commentForm) return;
    
    commentForm.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const recipeId = this.dataset.recipeId;
        const body = document.getElementById('commentBody').value;
        
        if (!body.trim()) return;
        
        try {
            const response = await fetch('/Recipe/AddComment', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ 
                    recipeId: parseInt(recipeId),
                    body: body.trim()
                })
            });
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    location.reload();
                }
            }
        } catch (error) {
            console.error('Error adding comment:', error);
        }
    });
}

// Initialize report button
function initializeReportButton() {
    const btnReport = document.getElementById('btnReportRecipe');
    if (!btnReport) return;
    
    btnReport.addEventListener('click', async function() {
        const reason = document.getElementById('reportReason').value;
        const recipeId = this.dataset.recipeId;
        
        if (!reason.trim()) {
            showAlertModal('Vui lòng nhập lý do báo cáo', 'warning');
            return;
        }
        
        try {
            const response = await fetch('/Recipe/ReportRecipe', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ 
                    recipeId: parseInt(recipeId),
                    reason: reason.trim()
                })
            });
            
            const result = await response.json();
            if (result.success) {
                const modal = bootstrap.Modal.getInstance(document.getElementById('reportRecipeModal'));
                if (modal) {
                    modal.hide();
                }
                document.getElementById('reportReason').value = '';
                showAlertModal(result.message, 'success');
            } else {
                showAlertModal(result.message, 'error');
            }
        } catch (error) {
            console.error('Error reporting recipe:', error);
            showAlertModal('Có lỗi xảy ra. Vui lòng thử lại.', 'error');
        }
    });
}

// Show image modal
function showImageModal(imageUrl) {
    document.getElementById('modalImage').src = imageUrl;
    new bootstrap.Modal(document.getElementById('imageModal')).show();
}

// Show alert modal
function showAlertModal(message, type) {
    const alertModal = document.getElementById('alertModal');
    const alertMessage = document.getElementById('alertMessage');
    const alertIcon = document.getElementById('alertIcon');
    const alertTitle = document.getElementById('alertTitle');
    
    if (!alertModal || !alertMessage || !alertIcon || !alertTitle) return;
    
    alertMessage.textContent = message;
    
    if (type === 'success') {
        alertIcon.className = 'fas fa-check-circle text-success';
        alertTitle.textContent = 'Thành công';
    } else if (type === 'error') {
        alertIcon.className = 'fas fa-times-circle text-danger';
        alertTitle.textContent = 'Lỗi';
    } else {
        alertIcon.className = 'fas fa-exclamation-triangle text-warning';
        alertTitle.textContent = 'Cảnh báo';
    }
    
    new bootstrap.Modal(alertModal).show();
}

