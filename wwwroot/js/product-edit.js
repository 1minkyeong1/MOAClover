// wwwroot/js/product-edit.js

document.addEventListener("DOMContentLoaded", function () {
    initEditCategorySelector();
    initEditMediaDeleteSync();
    initEditShippingInputs();
    initEditNewMediaPreview();
    initExistingMediaMoveButtons();
    initEditStockInputs();
});


// =========================
// 카테고리 4단계 선택
// =========================

function initEditCategorySelector() {
    const lv1 = document.getElementById("catLv1");
    const lv2 = document.getElementById("catLv2");
    const lv3 = document.getElementById("catLv3");
    const lv4 = document.getElementById("catLv4");
    const finalId = document.getElementById("FinalCategoryId");

    if (!finalId) {
        return;
    }

    function setFinalCategoryId() {
        finalId.value =
            (lv4 && lv4.value) ||
            (lv3 && lv3.value) ||
            (lv2 && lv2.value) ||
            (lv1 && lv1.value) ||
            "";
    }

    async function loadChildren(parentId, targetSelect) {
        if (!targetSelect) {
            return;
        }

        targetSelect.innerHTML = `<option value="">선택</option>`;

        if (!parentId) {
            targetSelect.disabled = true;
            return;
        }

        const res = await fetch(`/Home/GetChildCategories?parentId=${parentId}`);
        const data = await res.json();

        data.forEach(x => {
            const opt = document.createElement("option");
            opt.value = x.id;
            opt.textContent = x.name;
            targetSelect.appendChild(opt);
        });

        targetSelect.disabled = false;
    }

    lv1?.addEventListener("change", async () => {
        await loadChildren(lv1.value, lv2);

        if (lv3) {
            lv3.innerHTML = `<option value="">선택</option>`;
            lv3.disabled = true;
        }

        if (lv4) {
            lv4.innerHTML = `<option value="">선택</option>`;
            lv4.disabled = true;
        }

        setFinalCategoryId();
    });

    lv2?.addEventListener("change", async () => {
        await loadChildren(lv2.value, lv3);

        if (lv4) {
            lv4.innerHTML = `<option value="">선택</option>`;
            lv4.disabled = true;
        }

        setFinalCategoryId();
    });

    lv3?.addEventListener("change", async () => {
        await loadChildren(lv3.value, lv4);
        setFinalCategoryId();
    });

    lv4?.addEventListener("change", setFinalCategoryId);

    setFinalCategoryId();
}


// =========================
// 기존 미디어 활성 / 삭제 체크박스
// =========================

function initEditMediaDeleteSync() {
    document.querySelectorAll(".js-existing-media-card").forEach(card => {
        syncExistingMediaCard(card);

        const del = card.querySelector(".js-delete");

        if (del) {
            del.addEventListener("change", () => syncExistingMediaCard(card));
        }
    });
}

function syncExistingMediaCard(card) {
    const del = card.querySelector(".js-delete");
    const act = card.querySelector(".js-active");

    if (!del || !act) {
        return;
    }

    if (del.checked) {
        act.checked = false;
        act.disabled = true;
        card.classList.add("is-delete-marked");
    } else {
        act.disabled = false;
        card.classList.remove("is-delete-marked");
    }
}


// =========================
// 배송비 설정
// =========================

function initEditShippingInputs() {
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

        if (type === "Included" || type === "Free" || type === "Collect") {
            setDisabled(shippingFee, true, true);
            setDisabled(freeMin, true, true);
            setDisabled(jejuExtra, true, true);
            setDisabled(remoteExtra, true, true);
            return;
        }

        if (type === "Fixed") {
            setDisabled(shippingFee, false, false);
            setDisabled(freeMin, true, true);
            setDisabled(jejuExtra, false, false);
            setDisabled(remoteExtra, false, false);
            return;
        }

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
// 상품 수정 새 미디어 미리보기 / 삭제 / 순서변경
// =========================

const editNewMediaState = new Map();

function initEditNewMediaPreview() {
    document.querySelectorAll(".edit-new-media-input").forEach(input => {
        editNewMediaState.set(input.id, []);

        input.addEventListener("change", function () {
            const selectedFiles = Array.from(this.files || []);
            const currentFiles = editNewMediaState.get(this.id) || [];
            const mergedFiles = currentFiles.concat(selectedFiles);

            editNewMediaState.set(this.id, mergedFiles);
            syncEditNewMediaInput(this);
            renderEditNewMediaPreview(this);
        });

        renderEditNewMediaPreview(input);
    });
}

function renderEditNewMediaPreview(input) {
    const targetId = input.dataset.previewTarget;
    const mediaKind = input.dataset.mediaKind;
    const target = document.getElementById(targetId);

    if (!target) {
        return;
    }

    const files = editNewMediaState.get(input.id) || [];
    target.innerHTML = "";

    if (files.length === 0) {
        const empty = document.createElement("div");
        empty.className = "edit-new-media-empty";
        empty.innerText = "+";
        target.appendChild(empty);
        return;
    }

    files.forEach((file, index) => {
        const url = URL.createObjectURL(file);

        const card = document.createElement("div");
        card.className = "edit-new-media-card";

        const preview = mediaKind === "video"
            ? `<video src="${url}" controls></video>`
            : `<img src="${url}" alt="${escapeEditMediaHtml(file.name)}" />`;

        card.innerHTML = `
            <div class="edit-new-media-thumb">
                ${preview}
                <span class="edit-new-media-order">${index + 1}</span>
            </div>

            <div class="edit-new-media-name" title="${escapeEditMediaHtml(file.name)}">
                ${escapeEditMediaHtml(file.name)}
            </div>

            <div class="edit-new-media-actions">
                <button type="button"
                        onclick="moveEditNewMediaFile('${input.id}', ${index}, -1)"
                        ${index === 0 ? "disabled" : ""}>
                    ▲
                </button>

                <button type="button"
                        onclick="moveEditNewMediaFile('${input.id}', ${index}, 1)"
                        ${index === files.length - 1 ? "disabled" : ""}>
                    ▼
                </button>

                <button type="button"
                        class="delete"
                        onclick="removeEditNewMediaFile('${input.id}', ${index})">
                    삭제
                </button>
            </div>
        `;

        target.appendChild(card);
    });
}

function moveEditNewMediaFile(inputId, index, direction) {
    const input = document.getElementById(inputId);
    const files = editNewMediaState.get(inputId) || [];
    const nextIndex = index + direction;

    if (!input || nextIndex < 0 || nextIndex >= files.length) {
        return;
    }

    const temp = files[index];
    files[index] = files[nextIndex];
    files[nextIndex] = temp;

    editNewMediaState.set(inputId, files);
    syncEditNewMediaInput(input);
    renderEditNewMediaPreview(input);
}

function removeEditNewMediaFile(inputId, index) {
    const input = document.getElementById(inputId);
    const files = editNewMediaState.get(inputId) || [];

    if (!input) {
        return;
    }

    files.splice(index, 1);

    editNewMediaState.set(inputId, files);
    syncEditNewMediaInput(input);
    renderEditNewMediaPreview(input);
}

function syncEditNewMediaInput(input) {
    const files = editNewMediaState.get(input.id) || [];
    const dataTransfer = new DataTransfer();

    files.forEach(file => {
        dataTransfer.items.add(file);
    });

    input.files = dataTransfer.files;
}


// =========================
// 기존 미디어 카드 순서변경
// =========================

function initExistingMediaMoveButtons() {
    document.querySelectorAll(".edit-media-card-grid").forEach(grid => {
        const groups = new Set(
            Array.from(grid.querySelectorAll(".js-existing-media-card"))
                .map(card => card.dataset.mediaGroup)
        );

        groups.forEach(group => {
            updateExistingMediaMoveButtons(grid, group);
        });
    });
}

function moveExistingMediaCard(button, direction) {
    const card = button.closest(".js-existing-media-card");

    if (!card) {
        return;
    }

    const group = card.dataset.mediaGroup;
    const grid = card.parentElement;

    if (!grid) {
        return;
    }

    const cards = Array.from(
        grid.querySelectorAll(`.js-existing-media-card[data-media-group="${group}"]`)
    );

    const currentIndex = cards.indexOf(card);
    const nextIndex = currentIndex + direction;

    if (currentIndex < 0 || nextIndex < 0 || nextIndex >= cards.length) {
        return;
    }

    const targetCard = cards[nextIndex];

    card.classList.add("is-moving");

    if (direction < 0) {
        grid.insertBefore(card, targetCard);
    } else {
        grid.insertBefore(targetCard, card);
    }

    renumberExistingMediaGroup(grid, group);

    setTimeout(() => {
        card.classList.remove("is-moving");
    }, 250);
}

function renumberExistingMediaGroup(grid, group) {
    const cards = Array.from(
        grid.querySelectorAll(`.js-existing-media-card[data-media-group="${group}"]`)
    );

    cards.forEach((card, index) => {
        const sortInput = card.querySelector(".js-sort-order");
        const badge = card.querySelector(".edit-media-order-badge");

        if (sortInput) {
            sortInput.value = index;
        }

        if (badge) {
            badge.innerText = index;
        }
    });

    updateExistingMediaMoveButtons(grid, group);
}

function updateExistingMediaMoveButtons(grid, group) {
    const cards = Array.from(
        grid.querySelectorAll(`.js-existing-media-card[data-media-group="${group}"]`)
    );

    cards.forEach((card, index) => {
        const buttons = card.querySelectorAll(".edit-media-move-btn");

        if (buttons.length >= 2) {
            buttons[0].disabled = index === 0;
            buttons[1].disabled = index === cards.length - 1;
        }
    });
}

// =========================
// 재고 관리
// =========================

function initEditStockInputs() {
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


// =========================
// 공통
// =========================

function escapeEditMediaHtml(text) {
    return String(text)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}