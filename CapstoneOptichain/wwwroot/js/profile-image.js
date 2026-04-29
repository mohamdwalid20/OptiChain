// Profile Image Management
let stream = null;
let videoElement = null;

function openCamera() {
    if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
        navigator.mediaDevices.getUserMedia({ video: true })
            .then(function(mediaStream) {
                stream = mediaStream;
                showCameraModal();
            })
            .catch(function(error) {
                console.error('Camera access denied:', error);
                alert('Camera access denied. Please allow camera access or use file upload.');
            });
    } else {
        alert('Camera not supported in this browser. Please use file upload.');
    }
}

function showCameraModal() {
    const modal = document.createElement('div');
    modal.className = 'camera-modal';
    modal.innerHTML = `
        <div class="camera-modal-content">
            <div class="camera-header">
                <h3>Take Photo</h3>
                <button type="button" class="close-btn" onclick="closeCamera()">&times;</button>
            </div>
            <div class="camera-body">
                <video id="cameraVideo" autoplay playsinline></video>
                <canvas id="cameraCanvas" style="display: none;"></canvas>
            </div>
            <div class="camera-footer">
                <button type="button" class="btn-capture" onclick="capturePhoto()">
                    <i class="fas fa-camera"></i> Capture
                </button>
                <button type="button" class="btn-retake" onclick="retakePhoto()" style="display: none;">
                    <i class="fas fa-redo"></i> Retake
                </button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);

    videoElement = document.getElementById('cameraVideo');
    videoElement.srcObject = stream;
}

function capturePhoto() {
    const video = document.getElementById('cameraVideo');
    const canvas = document.getElementById('cameraCanvas');
    const context = canvas.getContext('2d');

    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    context.drawImage(video, 0, 0);

    // Convert canvas to blob and create file
    canvas.toBlob(function(blob) {
        const file = new File([blob], 'profile-photo.jpg', { type: 'image/jpeg' });
        const dataTransfer = new DataTransfer();
        dataTransfer.items.add(file);
        
        document.getElementById('profileImage').files = dataTransfer.files;
        updateImagePreview(URL.createObjectURL(blob));
        
        closeCamera();
    }, 'image/jpeg', 0.8);
}

function retakePhoto() {
    const canvas = document.getElementById('cameraCanvas');
    const video = document.getElementById('cameraVideo');
    const retakeBtn = document.querySelector('.btn-retake');
    const captureBtn = document.querySelector('.btn-capture');

    canvas.style.display = 'none';
    video.style.display = 'block';
    retakeBtn.style.display = 'none';
    captureBtn.style.display = 'block';
}

function closeCamera() {
    if (stream) {
        stream.getTracks().forEach(track => track.stop());
        stream = null;
    }
    const modal = document.querySelector('.camera-modal');
    if (modal) {
        modal.remove();
    }
}

function openFileUpload() {
    document.getElementById('profileImage').click();
}

function updateImagePreview(imageUrl) {
    const previewImg = document.getElementById('previewImg');
    const uploadPlaceholder = document.getElementById('uploadPlaceholder');
    
    previewImg.src = imageUrl;
    previewImg.style.display = 'block';
    uploadPlaceholder.style.display = 'none';
}

// Initialize profile image functionality when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    // Handle file input change
    const profileImageInput = document.getElementById('profileImage');
    if (profileImageInput) {
        profileImageInput.addEventListener('change', function(e) {
            const file = e.target.files[0];
            if (file) {
                if (file.type.startsWith('image/')) {
                    const reader = new FileReader();
                    reader.onload = function(e) {
                        updateImagePreview(e.target.result);
                    };
                    reader.readAsDataURL(file);
                } else {
                    alert('Please select an image file.');
                    this.value = '';
                }
            }
        });
    }

    // Handle image preview click
    const imagePreview = document.getElementById('imagePreview');
    if (imagePreview) {
        imagePreview.addEventListener('click', function() {
            document.getElementById('profileImage').click();
        });
    }
});
