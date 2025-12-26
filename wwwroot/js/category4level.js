(function () {
    const lv1 = document.getElementById('catLv1');
    const lv2 = document.getElementById('catLv2');
    const lv3 = document.getElementById('catLv3');
    const lv4 = document.getElementById('catLv4');
    const finalId = document.getElementById('FinalCategoryId');

    if (!lv1 || !lv2 || !lv3 || !lv4 || !finalId) return;

    function setFinalCategoryId() {
        finalId.value = lv4.value || lv3.value || lv2.value || lv1.value || "";
    }

    async function loadChildren(parentId, targetSelect) {
        targetSelect.innerHTML = `<option value="">선택</option>`;
        if (!parentId) {
            targetSelect.disabled = true;
            return;
        }
        const res = await fetch(`/Home/GetChildCategories?parentId=${parentId}`);
        const data = await res.json();
        data.forEach(x => {
            const opt = document.createElement('option');
            opt.value = x.id;
            opt.textContent = x.name;
            targetSelect.appendChild(opt);
        });
        targetSelect.disabled = false;
    }

    lv1.addEventListener('change', async () => {
        await loadChildren(lv1.value, lv2);
        lv3.innerHTML = `<option value="">선택</option>`; lv3.disabled = true;
        lv4.innerHTML = `<option value="">선택</option>`; lv4.disabled = true;
        setFinalCategoryId();
    });

    lv2.addEventListener('change', async () => {
        await loadChildren(lv2.value, lv3);
        lv4.innerHTML = `<option value="">선택</option>`; lv4.disabled = true;
        setFinalCategoryId();
    });

    lv3.addEventListener('change', async () => {
        await loadChildren(lv3.value, lv4);
        setFinalCategoryId();
    });

    lv4.addEventListener('change', () => setFinalCategoryId());

    setFinalCategoryId();
})();
