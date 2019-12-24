//Toggle Top Nav Menu
$("#navControl").click(function () {
    $("#navToggle").slideToggle("slow", function () {
        $("#mainStoreNav").toggleClass("menuOpen");
        $("#closeMenu").click(function () {
            $("#navToggle").slideUp("fast", function () {
                $("#mainStoreNav").removeClass("menuOpen");
            });
        });
    });
});

//SearchToggle - toggles search on mobile devices

$("#searchToggle").click(function () {
    $("#searchbar").slideToggle("fast", function () {
        $("#searchcloser").click(function () { $('#searchbar').slideUp("fast") });
    });
});

//Toggle menu items on mobile
$(".main-menu-item").click(function () {
    $(this).toggleClass('hello');
});