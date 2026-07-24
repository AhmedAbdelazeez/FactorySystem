const funnyBakeryMessages = [
    "جاري تجهيز العجينة...",
    "بنعدّ الباتيه واحدة واحدة 😄",
    "لحظة بنحسب عدد الباسكيت...",
    "جاري مراجعة المخزن...",
    "بنظبط الحسابات، ثانية واحدة...",
    "الشكارة دخلت الإنتاج، استنى الناتج 😄",
    "جاري حفظ البيانات...",
    "بنراجع السكر والزبدة...",
    "🥐 عجينتنا خمرت، بننفذ العملية!"
];

const funnyBakeryIcons = ["🥐", "🥖", "🍞", "🥨", "👨‍🍳", "🎂", "🧁"];

function showBakeryLoader(customMessage) {
    const loader = document.getElementById("bakeryLoader");
    const loaderMsg = document.getElementById("bakeryLoaderMessage");
    const loaderIcon = document.getElementById("bakeryLoaderIcon");

    if (loader) {
        const msg = customMessage || funnyBakeryMessages[Math.floor(Math.random() * funnyBakeryMessages.length)];
        const icon = funnyBakeryIcons[Math.floor(Math.random() * funnyBakeryIcons.length)];
        if (loaderMsg) loaderMsg.innerText = msg;
        if (loaderIcon) loaderIcon.innerText = icon;
        loader.style.display = "flex";
    }
}

function hideBakeryLoader() {
    const loader = document.getElementById("bakeryLoader");
    if (loader) {
        loader.style.display = "none";
    }
}

document.addEventListener("DOMContentLoaded", function () {
    // Intercept forms to display loader & prevent double submission
    const forms = document.querySelectorAll("form");
    forms.forEach(form => {
        form.addEventListener("submit", function (e) {
            const submitBtn = form.querySelector("button[type='submit']");
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm ms-1" role="status" aria-hidden="true"></span> جاري المعالجة...';
            }
            showBakeryLoader();
        });
    });
});
