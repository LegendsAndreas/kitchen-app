window.imageHelper = {
    resizeFile: function (file, maxWidth, maxHeight, quality) {
        return new Promise((resolve) => {
            let img = new Image();
            img.src = URL.createObjectURL(file);

            img.onload = function () {
                let canvas = document.createElement("canvas");
                let ctx = canvas.getContext("2d");

                let width = img.width;
                let height = img.height;

                if (width > maxWidth) {
                    height *= maxWidth / width;
                    width = maxWidth;
                }

                if (height > maxHeight) {
                    width *= maxHeight / height;
                    height = maxHeight;
                }

                canvas.width = width;
                canvas.height = height;

                ctx.drawImage(img, 0, 0, width, height);

                resolve(canvas.toDataURL("image/jpeg", quality));
            };
        });
    }
};