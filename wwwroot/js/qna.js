(function () {
    // ✅ 여러 번 로드/실행되어도 이벤트 중복 등록 방지
    if (window.__qnaBound) return;
    window.__qnaBound = true;

    function getRoot() {
        return document.getElementById("qna-root");
    }

    function getFormArea() {
        return document.getElementById("qna-form-area");
    }

    function getListArea() {
        return document.getElementById("qna-list-area");
    }

    function getProductId() {
        const root = getRoot();
        return root?.dataset?.productId;
    }

    function getToken() {
        return document.querySelector("#qnaForm input[name='__RequestVerificationToken']")?.value;
    }

    function renderList(html) {
        const listArea = getListArea();
        if (listArea) listArea.innerHTML = html;
        else console.warn("qna-list-area not found");
    }

    function showForm() {
        const formArea = getFormArea();
        if (!formArea) return;

        formArea.classList.remove("hidden");
        formArea.querySelector("textarea[name='Question']")?.focus();
    }

    function hideForm() {
        const formArea = getFormArea();
        if (!formArea) return;

        formArea.classList.add("hidden");
    }

    async function postJson(url, bodyObj) {
        const token = getToken();
        if (!token) {
            alert("토큰을 찾을 수 없습니다. (qnaForm / AntiForgeryToken 확인)");
            return null;
        }

        const res = await fetch(url, {
            method: "POST",
            headers: {
                "RequestVerificationToken": token,
                "Content-Type": "application/json"
            },
            body: JSON.stringify(bodyObj)
        });

        if (!res.ok) return null;
        return await res.text();
    }

    function init() {
        // ✅ root가 없어도 이벤트는 걸어둔다 (탭/부분렌더링 대비)
        const root = getRoot();
        if (!root) console.warn("qna-root not found (yet). Events are still bound.");

        // ✅ 클릭 이벤트 (closest로 버튼/자식요소 클릭 모두 잡기)
        document.addEventListener("click", function (e) {
            const showBtn = e.target.closest("#btnShowQnaForm");
            if (showBtn) {
                showForm();
                return;
            }

            const cancelBtn = e.target.closest("#btnCancelQnaForm");
            if (cancelBtn) {
                hideForm();
                return;
            }
        });

        // ✅ 등록 submit (위임)
        document.addEventListener("submit", async function (e) {
            const form = e.target;
            if (!form || form.id !== "qnaForm") return;

            e.preventDefault();

            const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (!token) return alert("토큰이 없습니다. (qnaForm 확인)");

            const formData = new FormData(form);

            const res = await fetch("/Home/AddQnAAjax", {
                method: "POST",
                headers: { "RequestVerificationToken": token },
                body: formData
            });

            if (!res.ok) return alert("문의 등록 중 오류가 발생했습니다.");

            renderList(await res.text());
            form.reset();   // 토큰은 보통 유지됨(초기값으로 리셋)
            hideForm();
        });

        // ✅ 아래부터 onclick에서 쓰므로 window에 등록
        window.loadQnAPage = async function (page) {
            const productId = getProductId();
            if (!productId) return alert("productId를 찾을 수 없습니다. (#qna-root data-product-id 확인)");

            const res = await fetch(`/Home/QnAPage?productId=${productId}&page=${page}`);
            if (!res.ok) return alert("Q&A 로딩 중 오류");

            renderList(await res.text());
        };

        window.deleteQnA = async function (id) {
            if (!confirm("정말 삭제하시겠습니까?")) return;

            const html = await postJson("/Home/DeleteQnA", { qnaId: id });
            if (!html) return alert("삭제 중 오류");

            renderList(html);
        };

        window.answerQnA = async function (id) {
            const textarea = document.getElementById(`answer_${id}`);
            const answer = textarea?.value?.trim();
            if (!answer) return alert("답변을 입력하세요.");

            const html = await postJson("/Home/AnswerQnA", { qnaId: id, answer });
            if (!html) return alert("답변 등록 오류");

            renderList(html);
        };

        window.startEditQnA = function (id) {
            document.getElementById(`edit_box_${id}`)?.classList.remove("hidden");
            document.getElementById(`edit_question_${id}`)?.focus();
        };

        window.cancelEditQnA = function (id) {
            document.getElementById(`edit_box_${id}`)?.classList.add("hidden");
        };

        window.saveEditQnA = async function (id) {
            const question = document.getElementById(`edit_question_${id}`)?.value?.trim();
            const isSecret = document.getElementById(`edit_secret_${id}`)?.checked ?? false;
            if (!question) return alert("내용을 입력하세요.");

            const html = await postJson("/Home/EditQnA", { qnaId: id, question, isSecret });
            if (!html) return alert("수정 중 오류");

            renderList(html);
        };

        window.startEditAnswer = function (id) {
            document.getElementById(`edit_answer_box_${id}`)?.classList.remove("hidden");
            document.getElementById(`edit_answer_${id}`)?.focus();
        };

        window.cancelEditAnswer = function (id) {
            document.getElementById(`edit_answer_box_${id}`)?.classList.add("hidden");
        };

        window.saveEditAnswer = async function (id) {
            const token = getToken();
            if (!token) return alert("토큰을 찾을 수 없습니다. (qnaForm 확인)");

            const answer = document.getElementById(`edit_answer_${id}`)?.value?.trim();
            if (!answer) return alert("답변을 입력하세요.");

            const res = await fetch("/Home/EditAnswer", {
                method: "POST",
                headers: {
                    "RequestVerificationToken": token,
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({ qnaId: id, answer })
            });

            if (!res.ok) return alert("답변 수정 오류");
            renderList(await res.text());
        };

        window.deleteAnswer = async function (id) {
            if (!confirm("답변을 삭제하시겠습니까?")) return;

            const token = getToken();
            if (!token) return alert("토큰을 찾을 수 없습니다. (qnaForm 확인)");

            const res = await fetch("/Home/DeleteAnswer", {
                method: "POST",
                headers: {
                    "RequestVerificationToken": token,
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({ qnaId: id })
            });

            if (!res.ok) return alert("답변 삭제 오류");
            renderList(await res.text());
        };


        console.log("qna.js init OK (fixed)");
    }

    // DOM 준비 후 실행
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
