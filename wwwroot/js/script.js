window.showStatusToast = function(toastId) {
    const toastElement = document.getElementById(toastId);
    if (toastElement) {
        const toastBootstrap = bootstrap.Toast.getOrCreateInstance(toastElement);
        toastBootstrap.show();
    } else {
        console.error('Toast element not found: ' + toastId);
    }
}