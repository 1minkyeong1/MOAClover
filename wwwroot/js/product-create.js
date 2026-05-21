// wwwroot/js/product-create.js

document.addEventListener("DOMContentLoaded", function () {
    initShippingInputs();
    initProductMediaPreview();
    initStockInputs();
});


// =========================
// 배송비 설정
// =========================

function initShippingInputs() {
    const shippingType = document.getElementById("ShippingType");
    const shippingFee = document.getElementById("ShippingFee");
    const freeMin = document.getElementById("FreeShippingMinAmount");
    const jejuExtra = document.getElementById("JejuExtraFee");
    const remoteExtra = document.getElementById("RemoteAreaExtraFee");

    if (!shippingType) {
        return;
    }

    function setDisabled(input, disabled, clearValue) {
        if (!input) {
            return;
        }

        input.disabled = disabled;

        if (disabled && clearValue) {
            input.value = "";
        }
    }

    function updateShippingInputs() {
        const type = shippingType.value;

        // 배송비 포함 / 무료배송 / 착불은 배송비 입력칸 필요 없음
        if (type === "Included" || type === "Free" || type === "Collect") {
            setDisabled(shippingFee, true, true);
            setDisabled(freeMin, true, true);
            setDisabled(jejuExtra, true, true);
            setDisabled(remoteExtra, true, true);
            return;
        }

        // 고정 배송비는 기본 배송비 + 지역 추가비만 사용
        if (type === "Fixed") {
            setDisabled(shippingFee, false, false);
            setDisabled(freeMin, true, true);
            setDisabled(jejuExtra, false, false);
            setDisabled(remoteExtra, false, false);
            return;
        }

        // 조건부 무료배송은 기본 배송비 + 무료배송 기준금액 + 지역 추가비 사용
        if (type === "ConditionalFree") {
            setDisabled(shippingFee, false, false);
            setDisabled(freeMin, false, false);
            setDisabled(jejuExtra, false, false);
            setDisabled(remoteExtra, false, false);
        }
    }

    shippingType.addEventListener("change", updateShippingInputs);
    updateShippingInputs();
}


// =========================
// 상품 등록 미디어 미리보기 / 삭제 / 순서변경
// =========================

const productMediaState = new Map();

function initProductMediaPreview() {
    document.querySelectorAll(".product-media-input").forEach(input => {
        productMediaState.set(input.id, []);

        input.addEventListener("change", function () {
            const selectedFiles = Array.from(this.files || []);
            const currentFiles = productMediaState.get(this.id) || [];
            const maxCount = Number(this.dataset.maxCount || 0);

            let mergedFiles = currentFiles.concat(selectedFiles);

            if (maxCount > 0 && mergedFiles.length > maxCount) {
                alert(`상단 썸네일은 최대 ${maxCount}장까지 등록할 수 있습니다.`);
                mergedFiles = mergedFiles.slice(0, maxCount);
            }

            productMediaState.set(this.id, mergedFiles);
            syncProductMediaInput(this);
            renderProductMediaPreview(this);
        });

        renderProductMediaPreview(input);
    });
}

function renderProductMediaPreview(input) {
    const targetId = input.dataset.previewTarget;
    const mediaKind = input.dataset.mediaKind;
    const target = document.getElementById(targetId);

    if (!target) {
        return;
    }

    const files = productMediaState.get(input.id) || [];
    target.innerHTML = "";

    if (files.length === 0) {
        const empty = document.createElement("div");
        empty.className = "product-media-empty";
        empty.innerText = "+";
        target.appendChild(empty);
        return;
    }

    files.forEach((file, index) => {
        const url = URL.createObjectURL(file);

        const card = document.createElement("div");
        card.className = "product-media-card";

        const preview = mediaKind === "video"
            ? `<video src="${url}" controls></video>`
            : `<img src="${url}" alt="${escapeProductMediaHtml(file.name)}" />`;

        const mainBadge = input.id === "ThumbImages" && index === 0
            ? `<span class="product-media-main">대표</span>`
            : "";

        card.innerHTML = `
            <div class="product-media-thumb">
                ${preview}
                <span class="product-media-order">${index + 1}</span>
                ${mainBadge}
            </div>

            <div class="product-media-name" title="${escapeProductMediaHtml(file.name)}">
                ${escapeProductMediaHtml(file.name)}
            </div>

            <div class="product-media-actions">
                <button type="button"
                        onclick="moveProductMediaFile('${input.id}', ${index}, -1)"
                        ${index === 0 ? "disabled" : ""}>
                    ▲
                </button>

                <button type="button"
                        onclick="moveProductMediaFile('${input.id}', ${index}, 1)"
                        ${index === files.length - 1 ? "disabled" : ""}>
                    ▼
                </button>

                <button type="button"
                        class="delete"
                        onclick="removeProductMediaFile('${input.id}', ${index})">
                    삭제
                </button>
            </div>
        `;

        target.appendChild(card);
    });
}

function moveProductMediaFile(inputId, index, direction) {
    const input = document.getElementById(inputId);
    const files = productMediaState.get(inputId) || [];
    const nextIndex = index + direction;

    if (!input || nextIndex < 0 || nextIndex >= files.length) {
        return;
    }

    const temp = files[index];
    files[index] = files[nextIndex];
    files[nextIndex] = temp;

    productMediaState.set(inputId, files);
    syncProductMediaInput(input);
    renderProductMediaPreview(input);
}

function removeProductMediaFile(inputId, index) {
    const input = document.getElementById(inputId);
    const files = productMediaState.get(inputId) || [];

    if (!input) {
        return;
    }

    files.splice(index, 1);

    productMediaState.set(inputId, files);
    syncProductMediaInput(input);
    renderProductMediaPreview(input);
}

function syncProductMediaInput(input) {
    const files = productMediaState.get(input.id) || [];
    const dataTransfer = new DataTransfer();

    files.forEach(file => {
        dataTransfer.items.add(file);
    });

    input.files = dataTransfer.files;
}

function escapeProductMediaHtml(text) {
    return String(text)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

// =========================
// 재고 관리
// =========================

function initStockInputs() {
    const useStock = document.getElementById("UseStock");
    const stockQuantity = document.getElementById("StockQuantity");

    if (!useStock || !stockQuantity) {
        return;
    }

    function updateStockInput() {
        stockQuantity.disabled = !useStock.checked;

        if (!useStock.checked) {
            stockQuantity.value = "0";
        }
    }

    useStock.addEventListener("change", updateStockInput);
    updateStockInput();
}