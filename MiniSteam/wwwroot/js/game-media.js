(() => {
    const gallery = document.getElementById("gameMediaGallery");
    if (!gallery) {
        return;
    }

    const thumbnails = Array.from(gallery.querySelectorAll(".game-media-thumbnail"));
    const mainImage = document.getElementById("gameMainMediaImage");
    const mainFrame = document.getElementById("gameMainMediaFrame");
    const mainVideo = document.getElementById("gameMainMediaVideo");
    const stage = document.getElementById("gameMediaStage");
    const expandButton = document.getElementById("gameMediaExpand");

    const lightbox = document.getElementById("gameMediaLightbox");
    const lightboxImage = document.getElementById("gameLightboxImage");
    const lightboxFrame = document.getElementById("gameLightboxFrame");
    const lightboxVideo = document.getElementById("gameLightboxVideo");

    const items = thumbnails.map(button => ({
        type: button.dataset.mediaType || "image",
        url: button.dataset.mediaUrl || "",
        button
    })).filter(item => item.url);

    if (items.length === 0 && mainImage?.src) {
        items.push({
            type: "image",
            url: mainImage.src,
            button: null
        });
    }

    let currentIndex = Number.parseInt(gallery.dataset.initialIndex || "0", 10);
    if (!Number.isFinite(currentIndex) || currentIndex < 0 || currentIndex >= items.length) {
        currentIndex = 0;
    }

    const navigationButtons = [
        gallery.querySelector("[data-media-prev]"),
        gallery.querySelector("[data-media-next]"),
        lightbox?.querySelector("[data-lightbox-prev]"),
        lightbox?.querySelector("[data-lightbox-next]")
    ].filter(Boolean);

    if (items.length <= 1) {
        navigationButtons.forEach(button => button.hidden = true);
    }

    const stopMainVideo = () => {
        if (mainFrame) {
            mainFrame.src = "";
        }

        if (mainVideo) {
            mainVideo.pause();
            mainVideo.removeAttribute("src");
            mainVideo.load();
        }
    };

    const stopLightboxVideo = () => {
        if (lightboxFrame) {
            lightboxFrame.src = "";
        }

        if (lightboxVideo) {
            lightboxVideo.pause();
            lightboxVideo.removeAttribute("src");
            lightboxVideo.load();
        }
    };

    const hideMainElements = () => {
        if (mainImage) mainImage.hidden = true;
        if (mainFrame) mainFrame.hidden = true;
        if (mainVideo) mainVideo.hidden = true;
    };

    const hideLightboxElements = () => {
        if (lightboxImage) lightboxImage.hidden = true;
        if (lightboxFrame) lightboxFrame.hidden = true;
        if (lightboxVideo) lightboxVideo.hidden = true;
    };

    const markActiveThumbnail = () => {
        thumbnails.forEach(button => button.classList.remove("active"));
        const current = items[currentIndex];
        if (current?.button) {
            current.button.classList.add("active");
            current.button.setAttribute("aria-current", "true");
            thumbnails
                .filter(button => button !== current.button)
                .forEach(button => button.removeAttribute("aria-current"));
        }
    };

    const showMainItem = (index, autoplay = false) => {
        if (items.length === 0) return;

        currentIndex = (index + items.length) % items.length;
        const item = items[currentIndex];

        stage?.classList.add("is-switching");

        window.setTimeout(() => {
            stopMainVideo();
            hideMainElements();

            if (item.type === "image") {
                if (mainImage) {
                    mainImage.src = item.url;
                    mainImage.hidden = false;
                    mainImage.alt = "Selected game screenshot";
                }

                if (expandButton) expandButton.hidden = false;
            } else if (item.type === "youtube") {
                if (mainFrame) {
                    const separator = item.url.includes("?") ? "&" : "?";
                    mainFrame.src = autoplay ? `${item.url}${separator}autoplay=1` : item.url;
                    mainFrame.hidden = false;
                }

                if (expandButton) expandButton.hidden = true;
            } else {
                if (mainVideo) {
                    mainVideo.src = item.url;
                    mainVideo.hidden = false;
                    if (autoplay) {
                        mainVideo.play().catch(() => { });
                    }
                }

                if (expandButton) expandButton.hidden = true;
            }

            markActiveThumbnail();
            stage?.classList.remove("is-switching");
        }, 120);
    };

    const showLightboxItem = index => {
        if (!lightbox || items.length === 0) return;

        currentIndex = (index + items.length) % items.length;
        const item = items[currentIndex];

        stopLightboxVideo();
        hideLightboxElements();

        if (item.type === "image") {
            if (lightboxImage) {
                lightboxImage.src = item.url;
                lightboxImage.hidden = false;
            }
        } else if (item.type === "youtube") {
            if (lightboxFrame) {
                const separator = item.url.includes("?") ? "&" : "?";
                lightboxFrame.src = `${item.url}${separator}autoplay=1`;
                lightboxFrame.hidden = false;
            }
        } else if (lightboxVideo) {
            lightboxVideo.src = item.url;
            lightboxVideo.hidden = false;
            lightboxVideo.play().catch(() => { });
        }

        markActiveThumbnail();
        showMainItem(currentIndex, false);
    };

    const openLightbox = () => {
        if (!lightbox || items.length === 0 || items[currentIndex]?.type !== "image") {
            return;
        }

        showLightboxItem(currentIndex);
        lightbox.hidden = false;
        lightbox.setAttribute("aria-hidden", "false");
        document.body.classList.add("media-lightbox-open");
        lightbox.querySelector("[data-lightbox-close]")?.focus();
    };

    const closeLightbox = () => {
        if (!lightbox || lightbox.hidden) return;

        stopLightboxVideo();
        lightbox.hidden = true;
        lightbox.setAttribute("aria-hidden", "true");
        document.body.classList.remove("media-lightbox-open");
        gallery.focus();
    };

    const move = direction => {
        if (items.length <= 1) return;
        const nextIndex = (currentIndex + direction + items.length) % items.length;

        if (lightbox && !lightbox.hidden) {
            showLightboxItem(nextIndex);
        } else {
            showMainItem(nextIndex, false);
        }
    };

    thumbnails.forEach((button, index) => {
        button.addEventListener("click", () => {
            showMainItem(index, button.dataset.mediaType !== "image");
        });
    });

    gallery.querySelector("[data-media-prev]")?.addEventListener("click", () => move(-1));
    gallery.querySelector("[data-media-next]")?.addEventListener("click", () => move(1));

    expandButton?.addEventListener("click", openLightbox);
    mainImage?.addEventListener("click", openLightbox);

    lightbox?.querySelector("[data-lightbox-close]")?.addEventListener("click", closeLightbox);
    lightbox?.querySelector("[data-lightbox-prev]")?.addEventListener("click", () => move(-1));
    lightbox?.querySelector("[data-lightbox-next]")?.addEventListener("click", () => move(1));

    lightbox?.addEventListener("click", event => {
        if (event.target === lightbox) {
            closeLightbox();
        }
    });

    document.addEventListener("keydown", event => {
        const lightboxOpen = lightbox && !lightbox.hidden;
        const galleryFocused = gallery.contains(document.activeElement) || document.activeElement === gallery;

        if (event.key === "Escape" && lightboxOpen) {
            event.preventDefault();
            closeLightbox();
            return;
        }

        if (!lightboxOpen && !galleryFocused) {
            return;
        }

        if (event.key === "ArrowLeft") {
            event.preventDefault();
            move(-1);
        } else if (event.key === "ArrowRight") {
            event.preventDefault();
            move(1);
        } else if (event.key === "Enter" && !lightboxOpen && items[currentIndex]?.type === "image") {
            event.preventDefault();
            openLightbox();
        }
    });

    if (items.length > 0) {
        showMainItem(currentIndex, false);
    }
})();
