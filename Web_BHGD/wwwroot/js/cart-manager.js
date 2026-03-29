/**
 * Cart Manager - Quản lý giỏ hàng toàn cục
 * Sử dụng: Tích hợp vào _Layout.cshtml để áp dụng cho tất cả trang
 */

window.CartManager = {
    // Configuration
    config: {
        addToCartUrl: '',
        getCartCountUrl: '',
        toastDuration: 3000,
        maxToasts: 3 // Số lượng toast tối đa hiển thị cùng lúc
    },

    // Khởi tạo CartManager
    init: function (addToCartUrl, getCartCountUrl) {
        this.config.addToCartUrl = addToCartUrl;
        this.config.getCartCountUrl = getCartCountUrl;

        // Cập nhật số lượng giỏ hàng khi khởi tạo
        this.updateCartCount();

        // Bind events
        this.bindEvents();

        console.log('CartManager initialized');
    },

    // Bind các events
    bindEvents: function () {
        var self = this;

        // Xử lý click nút "Thêm vào giỏ"
        $(document).on('click', '.add-to-cart-btn', function (e) {
            e.preventDefault();
            self.handleAddToCartClick($(this));
        });

        // Xử lý form tìm kiếm (trong product page)
        $(document).on('submit', '.product-search-form', function (e) {
            self.handleSearchSubmit($(this), e);
        });
    },

    // Xử lý click nút thêm vào giỏ
    handleAddToCartClick: function (btn) {
        var productId = btn.data('product-id');
        var productName = btn.data('product-name');
        var quantity = btn.data('quantity') || 1;

        if (!productId || productId <= 0) {
            this.showToast('error', 'ID sản phẩm không hợp lệ.');
            return;
        }

        var originalHtml = btn.html();
        this.setButtonLoading(btn, true);

        this.addToCart(productId, productName, quantity)
            .always(function () {
                CartManager.setButtonLoading(btn, false, originalHtml);
            });
    },

    // Xử lý submit form tìm kiếm
    handleSearchSubmit: function (form, event) {
        var searchTerm = form.find('input[name="searchString"]').val().trim();
        if (!searchTerm) {
            event.preventDefault();
            this.showToast('error', 'Vui lòng nhập từ khóa tìm kiếm.');
        }
    },

    // Set trạng thái loading cho button
    setButtonLoading: function (btn, isLoading, originalHtml) {
        if (isLoading) {
            btn.prop('disabled', true)
                .html('<i class="spinner-border spinner-border-sm me-1"></i> Đang thêm...');
        } else {
            btn.prop('disabled', false)
                .html(originalHtml || '<i class="bi bi-cart-plus me-1"></i>Thêm vào giỏ');
        }
    },

    // Cập nhật số lượng giỏ hàng
    updateCartCount: function () {
        var self = this;

        if (!this.config.getCartCountUrl) {
            console.warn('getCartCountUrl not configured');
            return;
        }

        $.get(this.config.getCartCountUrl)
            .done(function (data) {
                $('.cart-count').text(data.count || 0);
            })
            .fail(function (xhr, status, error) {
                console.error('Failed to update cart count:', { status, error });
                $('.cart-count').text('0');
            });
    },

    // Thêm sản phẩm vào giỏ hàng
    addToCart: function (productId, productName, quantity) {
        var self = this;
        quantity = quantity || 1;

        if (!this.config.addToCartUrl) {
            console.error('addToCartUrl not configured');
            this.showToast('error', 'Cấu hình hệ thống không đúng.');
            return $.Deferred().reject();
        }

        return $.ajax({
            url: this.config.addToCartUrl,
            type: 'POST',
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            data: {
                productId: productId,
                quantity: quantity,
                __RequestVerificationToken: this.getAntiForgeryToken()
            }
        })
            .done(function (response) {
                self.handleAddToCartSuccess(response, productName);
            })
            .fail(function (xhr, status, error) {
                self.handleAddToCartError(xhr, status, error);
            });
    },

    // Xử lý khi thêm vào giỏ thành công
    handleAddToCartSuccess: function (response, productName) {
        if (typeof response === 'object' && response.success === false) {
            this.showToast('error', response.message || 'Có lỗi xảy ra khi thêm sản phẩm.');
            return;
        }

        var message = (typeof response === 'object' && response.message)
            ? response.message
            : `Đã thêm ${productName} vào giỏ hàng.`;

        this.showToast('success', message);
        this.updateCartCount();
    },

    // Xử lý khi thêm vào giỏ lỗi
    handleAddToCartError: function (xhr, status, error) {
        console.error('Add to cart error:', { status, error, responseText: xhr.responseText });

        var errorMessage = this.getErrorMessage(xhr.status);
        this.showToast('error', errorMessage);
    },

    // Lấy thông báo lỗi dựa trên status code
    getErrorMessage: function (statusCode) {
        var messages = {
            400: 'Dữ liệu không hợp lệ.',
            404: 'Không tìm thấy sản phẩm.',
            500: 'Lỗi server. Vui lòng thử lại sau.',
            0: 'Mất kết nối mạng.'
        };

        return messages[statusCode] || 'Lỗi khi thêm sản phẩm vào giỏ hàng.';
    },

    // Lấy CSRF token
    getAntiForgeryToken: function () {
        return $('input[name="__RequestVerificationToken"]').val() || '';
    },

    // Quản lý số lượng toast hiển thị
    manageToasts: function () {
        var toasts = $('#toast-container .alert');
        if (toasts.length > this.config.maxToasts) {
            // Xóa toast cũ nhất nếu vượt quá số lượng cho phép
            toasts.first().alert('close');
        }
    },

    // Hiển thị thông báo toast với container tập trung
    showToast: function (type, message) {
        var toastClass = type === 'success' ? 'alert-success' : 'alert-danger';
        var icon = type === 'success' ? 'bi-check-circle' : 'bi-exclamation-triangle';
        var toastId = 'toast-' + Date.now();

        // Đảm bảo container tồn tại
        if ($('#toast-container').length === 0) {
            $('body').append('<div id="toast-container" class="position-fixed" style="top: 80px; right: 20px; z-index: 9999; width: 350px;"></div>');
        }

        var toast = `
            <div id="${toastId}" class="alert ${toastClass} alert-dismissible fade show mb-2" role="alert">
                <i class="bi ${icon} me-2"></i>${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>`;

        // Thêm toast vào container
        $('#toast-container').append(toast);

        // Quản lý số lượng toast
        this.manageToasts();

        // Tự động ẩn toast
        setTimeout(function () {
            $('#' + toastId).alert('close');
        }, this.config.toastDuration);

        // Animation khi toast xuất hiện
        $('#' + toastId).hide().slideDown(300);

        // Xử lý khi toast bị đóng
        $('#' + toastId).on('closed.bs.alert', function () {
            $(this).slideUp(200, function () {
                $(this).remove();
            });
        });
    },

    // Utility methods
    utils: {
        // Format số tiền
        formatCurrency: function (amount) {
            return new Intl.NumberFormat('vi-VN').format(amount) + '₫';
        },

        // Validate email
        isValidEmail: function (email) {
            var re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            return re.test(email);
        },

        // Validate phone
        isValidPhone: function (phone) {
            var re = /^[0-9]{10,11}$/;
            return re.test(phone.replace(/\s/g, ''));
        }
    }
};