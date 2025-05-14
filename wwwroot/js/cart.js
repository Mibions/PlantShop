function updateCartCount() {
    $.get('/Cart/GetCartCount', function (count) {
        $('.cart-count').text(count);
    });
}

$(document).ready(function () {
    // Cập nhật số lượng sản phẩm khi trang được tải
    updateCartCount();

    // Thay thế sự kiện submit bằng delegation
    $(document).off('submit', '.add-to-cart-form').on('submit', '.add-to-cart-form', function (e) {
        e.preventDefault();
        console.log("Form submitted");

        var form = $(this);
        var plantId = form.find('input[name="plantId"]').val();
        var quantity = parseInt(form.find('input[name="quantity"]').val()) || 1;
        var button = form.find('button[type="submit"]');

        console.log("PlantId:", plantId, "Quantity:", quantity);

        // Disable nút thêm vào giỏ để tránh click nhiều lần
        button.prop('disabled', true);

        $.ajax({
            url: '/Cart/AddToCart',
            type: 'POST',
            data: {
                plantId: plantId,
                quantity: quantity
            },
            success: function (response) {
                console.log("Success:", response);
                updateCartCount();
                if (window.bootstrap && document.getElementById('cartToast')) {
                    var toast = new bootstrap.Toast(document.getElementById('cartToast'));
                    toast.show();
                }
            },
            error: function (xhr, status, error) {
                console.error("Error:", error, xhr.responseText);
                var message = 'Có lỗi xảy ra khi thêm vào giỏ hàng!';
                if (xhr.responseText) {
                    message = xhr.responseText;
                }
                alert(message);
            },
            complete: function () {
                // Enable lại nút thêm vào giỏ
                button.prop('disabled', false);
            }
        });
    });

    // Thêm sự kiện change cho input số lượng
    $(document).on('change', '.quantity-input', function () {
        console.log("Quantity changed to:", $(this).val());
    });

    // Xử lý cập nhật số lượng
    $(document).on('click', '.update-quantity', function (e) {
        e.preventDefault();
        var button = $(this);
        var plantId = button.data('plant-id');
        var quantity = button.data('quantity');

        if (quantity < 1) return;

        button.prop('disabled', true);

        $.ajax({
            url: '/Cart/UpdateQuantity',
            type: 'POST',
            data: {
                plantId: plantId,
                quantity: quantity
            },
            success: function (response) {
                // Cập nhật nội dung giỏ hàng
                $('#cartPopup .offcanvas-body').html($(response).find('.offcanvas-body').html());
                // Cập nhật số lượng sản phẩm
                updateCartCount();
            },
            error: function (xhr) {
                var message = 'Có lỗi xảy ra khi cập nhật số lượng!';
                if (xhr.responseText) {
                    message = xhr.responseText;
                }
                alert(message);
            },
            complete: function () {
                button.prop('disabled', false);
            }
        });
    });

    // Xử lý xóa sản phẩm
    $(document).on('click', '.remove-from-cart', function (e) {
        e.preventDefault();
        var button = $(this);
        var plantId = button.data('plant-id');

        if (confirm('Bạn có chắc chắn muốn xóa sản phẩm này?')) {
            button.prop('disabled', true);

            $.ajax({
                url: '/Cart/RemoveFromCart',
                type: 'POST',
                data: {
                    plantId: plantId
                },
                success: function (response) {
                    // Sau khi xóa thành công, reload lại trang giỏ hàng
                    location.reload();
                },
                error: function (xhr) {
                    var message = 'Có lỗi xảy ra khi xóa sản phẩm!';
                    if (xhr.responseText) {
                        message = xhr.responseText;
                    }
                    alert(message);
                },
                complete: function () {
                    button.prop('disabled', false);
                }
            });
        }
    });
}); 