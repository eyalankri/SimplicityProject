(function ($, cookieHelper) {

    $(document).ready(function () {

        var dependencies = [
            {
                module: "header",
                dependencies: ["attachDetach", "overlay", "perfectScrollbar"]
            },
            {
                module: "menu",
                dependencies: ["perfectScrollbar"]
            }
        ];

        var themeSettings = {
            //lazyLoadProductBoxImages: {
            //    selector: '.item-box .picture a img[data-lazyloadsrc]'
            //},
            goToTop: {
                scrollElementSelector: '#goToTop'
            },
            overlay: {
                overlayElementSelector: '.overlayOffCanvas',
                overlayClass: 'show',
                noPageScrollClass: 'scrollYRemove'
            },
            productQuantity: {
                quantityInputSelector: '.qty-input, .productQuantityTextBox',
                incrementSelectors: '.plus',
                decrementSelectors: '.minus'
            },
            toggle: {
                blocks: [
                    {
                        opener: '.search-opener',
                        content: '.store-search-box form',
                        activeClassOpener: 'open',
                        activeClassContent: 'open',
                        animation: {
                            type: 'none'
                        },
                        afterToggle: function () {

                            $(".header-lower").toggleClass("search-overlay-open");
                        }
                    },
                    {
                        opener: '.footer-map-holder .title',
                        content: '.footer-map-wrapper',
                        activeClassOpener: 'open',
                        animation: {
                            type: 'slide',
                            speed: 'slow'
                        },
                        afterToggle: function () {

                            window.google.maps.event.trigger(window.footerMap, "resize");
                        }
                    },
                    {
                        opener: '.sublist-toggle-button',
                        content: '.sublist',
                        activeClassOpener: 'open',
                        animation: {
                            type: 'slide',
                            speed: 'slow'
                        }
                    },
                    {
                        opener: '.new-comment .title',
                        content: '.new-comment form',
                        activeClassOpener: 'open',
                        animation: {
                            type: 'slide',
                            speed: 'slow'
                        }
                    },
                    {
                        opener: '.write-review .title',
                        content: '.write-review-collapse',
                        activeClassOpener: 'open',
                        animation: {
                            type: 'slide',
                            speed: 'slow'
                        }
                    }
                ]
            },
            flyoutCart: {
                flyoutCartSelector: '#flyout-cart',
                removeItemSelector: '.mini-shopping-cart-item-close'
            },
            equalizer: {
                blocks: [
                    {
                        selector: '.checkout-page .address-item'
                    },
                    {
                        selector: '.rich-blog-homepage .blog-post'
                    }
                ]
            },
            responsive: [
                {
                    breakpoint: 1201,
                    settings: {
                        menu: {
                            sublistIndent: {
                                enabled: true,
                                topLevelMenuWidth: 360
                            }
                        },
                        header: {
                            activeClass: 'open',
                            modules: [
                                {
                                    content: '.header-selectors-wrapper',
                                    elementToAttach: '.mobile-menu-items'
                                },
                                {
                                    content: '#CartQuantityBox',
                                    elementToAttach: '.responsive-nav-wrapper .shopping-cart-link a'
                                },
	                            {
	                                opener: '.search-wrap > span',
	                                content: '.store-search-box',
	                                elementToAttach: '.master-wrapper-page',
	                                overlay: true
	                            },
                                {
                                    opener: '.responsive-nav-wrapper .menu-title > span',
                                    closer: '.close-menu > span',
                                    content: '.header-menu',
                                    overlay: true,
                                    scrollbar: true
                                },
                                {
                                    elementToAttach: '.master-wrapper-page',
                                    opener: '.filters-button-wrapper .filters-button',
                                    closer: '.nopAjaxFilters7Spikes .close-btn',
                                    content: '.nopAjaxFilters7Spikes',
                                    overlay: true,
                                    scrollbar: true
                                }
                            ]
                        },
                        toggle: {
                            blocks: [
                                {
                                    opener: '.block .title',
                                    content: '.listbox',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.mobile-selector .selector-title',
                                    content: '.mobile-collapse',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.footer-map-holder .title',
                                    content: '.footer-map-wrapper',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    },
                                    afterToggle: function () {

                                        window.google.maps.event.trigger(window.footerMap, "resize");
                                    }
                                },
                                {
                                    opener: '.new-comment .title',
                                    content: '.new-comment form',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.write-review .title',
                                    content: '.write-review-collapse',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                }
                            ]
                        },
                        equalizer: {
                            blocks: [
                                {
                                    selector: '.cart tr'
                                },
                                {
                                    selector: '.checkout-page .address-item'
                                }
                            ]
                        }
                    }
                },
                {
                    breakpoint: 769,
                    settings: {
                        menu: {
                            sublistIndent: {
                                enabled: true,
                                topLevelMenuWidth: 360
                            }
                        },
                        header: {
                            activeClass: 'open',
                            modules: [
                                {
                                    content: '.header-selectors-wrapper',
                                    elementToAttach: '.mobile-menu-items'
                                },
                                {
                                    content: '#CartQuantityBox',
                                    elementToAttach: '.responsive-nav-wrapper .shopping-cart-link a'
                                },
	                            {
	                                opener: '.search-wrap > span',
	                                content: '.store-search-box',
	                                elementToAttach: '.master-wrapper-page',
	                                overlay: true
	                            },
                                {
                                    opener: '.responsive-nav-wrapper .menu-title > span',
                                    closer: '.close-menu > span',
                                    content: '.header-menu',
                                    overlay: true,
                                    scrollbar: true
                                },
                                {
                                    elementToAttach: '.master-wrapper-page',
                                    opener: '.filters-button-wrapper .filters-button',
                                    closer: '.nopAjaxFilters7Spikes .close-btn',
                                    content: '.nopAjaxFilters7Spikes',
                                    overlay: true,
                                    scrollbar: true
                                }
                            ]
                        },
                        toggle: {
                            blocks: [
                                {
                                    opener: '.block .title',
                                    content: '.listbox',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.mobile-selector .selector-title',
                                    content: '.mobile-collapse',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.footer-block .title',
                                    content: '.footer-collapse',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.footer-map-holder .title',
                                    content: '.footer-map-wrapper',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    },
                                    afterToggle: function () {

                                        window.google.maps.event.trigger(window.footerMap, "resize");
                                    }
                                },
                                {
                                    opener: '.sub-category-toggle-select',
                                    content: '.sub-categories-wrapper',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.cart-collaterals .title',
                                    content: '',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.new-comment .title',
                                    content: '.new-comment form',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.write-review .title',
                                    content: '.write-review-collapse',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                }
                            ]
                        },
                        equalizer: {
                            blocks: [
                                {
                                    selector: '.cart tr'
                                },
                                {
                                    selector: '.checkout-page .address-item'
                                }
                            ]
                        }
                    }
                },
                {
                    breakpoint: 481,
                    settings: {
                        menu: {
                            sublistIndent: {
                                enabled: true,
                                topLevelMenuWidth: 320
                            }
                        },
                        header: {
                            activeClass: 'open',
                            modules: [
                                {
                                    content: '.header-selectors-wrapper',
                                    elementToAttach: '.mobile-menu-items'
                                },
                                {
                                    content: '#CartQuantityBox',
                                    elementToAttach: '.responsive-nav-wrapper .shopping-cart-link a'
                                },
                                {
                                    opener: '.search-wrap > span',
                                    content: '.store-search-box',
                                    elementToAttach: '.master-wrapper-page',
                                    overlay: true
                                },
                                {
                                    opener: '.responsive-nav-wrapper .menu-title > span',
                                    closer: '.close-menu > span',
                                    content: '.header-menu',
                                    overlay: true,
                                    scrollbar: true
                                },
                                {
                                    elementToAttach: '.master-wrapper-page',
                                    opener: '.filters-button-wrapper .filters-button',
                                    closer: '.nopAjaxFilters7Spikes .close-btn',
                                    content: '.nopAjaxFilters7Spikes',
                                    overlay: true,
                                    scrollbar: true
                                }
                            ]
                        },
                        toggle: {
                            blocks: [
                                {
                                    opener: '.block .title',
                                    content: '.listbox',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.mobile-selector .selector-title',
                                    content: '.mobile-collapse',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.footer-block .title',
                                    content: '.footer-collapse',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.footer-map-holder .title',
                                    content: '.footer-map-wrapper',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    },
                                    afterToggle: function () {

                                        window.google.maps.event.trigger(window.footerMap, "resize");
                                    }
                                },
                                {
                                    opener: '.sub-category-toggle-select',
                                    content: '.sub-categories-wrapper',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.cart-collaterals .title',
                                    content: '',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.new-comment .title',
                                    content: '.new-comment form',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                },
                                {
                                    opener: '.write-review .title',
                                    content: '.write-review-collapse',
                                    activeClassOpener: 'open',
                                    animation: {
                                        type: 'slide',
                                        speed: 'slow'
                                    }
                                }
                            ]
                        },
                        equalizer: {
                            blocks: [
                                {
                                    selector: '.checkout-page .address-item'
                                }
                            ]
                        }
                    }
                }
            ]
        };

        var theme = new window.sevenSpikesTheme(themeSettings, dependencies, false);

        theme.init();

        handleCategoryNavigationCollapse();
        handleClearCartButton();
        handleAddNewAddressExpand();
        handleNewCommetnExpand();
        handleWriteReviewExpand();
        handleGridViewModes();
        handleAjaxFiltersPriceRangeHandels();
        handleHomePageVideoPlayer();
        
    });

    $(document).on('ventureMapLoaded', function () {

        $(".footer-map-holder .footer-map-wrapper").hide();
    });

    $(document).on("quickTabsRefreshedTab", function () {
        handleWriteReviewExpand();
    });

    $(window).on('load', function () {
        $('.page-loader').fadeOut("slow");
    });

    function handleCategoryNavigationCollapse() {

        var sublistOpenButton = $(".sublist-toggle-button");

        sublistOpenButton.each(function () {
            if ($(this).siblings(".sublist").css("display") !== "none") {
                $(this).addClass("open");
            }
        });
    }

    function handleClearCartButton() {
        $('.order-summary-content .clear-cart-button').on('click', function (e) {
            e.preventDefault();

            $('.cart [name="removefromcart"]').attr('checked', 'checked');

            $('.order-summary-content .update-cart-button').click();
        });
    }

    function handleAddNewAddressExpand() {

        if ($(".address-section-collapse .field-validation-error").length > 0) {
            $(".address-section-collapse").slideDown('slow', function() {

                $('html, body').animate({
                    scrollTop: $(".address-section-collapse").offset().top
                }, 0);
            });

            $(".checkout-page .enter-new-address").addClass("open");
        }

        $(".checkout-page .enter-new-address").click(function () {
            $(".address-section-collapse").slideToggle('slow');

            $(".checkout-page .enter-new-address").toggleClass("open");
        });
    }

    function handleNewCommetnExpand() {

        if ($(".new-comment .field-validation-error").length > 0) {
            $(".new-comment form").slideDown('slow', function () {

                $('html, body').animate({
                    scrollTop: $(".new-comment").offset().top
                }, 0);
            });

            $(".new-comment title").addClass("open");
        }
    }

    function handleWriteReviewExpand() {

        if ($(".write-review .field-validation-error").length > 0) {
            $(".write-review-collapse").slideDown('slow', function () {

                $('html, body').animate({
                    scrollTop: $(".write-review-collapse").offset().top
                }, 0);
            });

            $(".write-review title").addClass("open");
        }
    }

    function handleGridViewModes() {

        var gridViewModeSelector = $('.product-viewmode .grid').not('.hidden');
        var listViewModeSelector = $('.product-viewmode .list').not('.hidden');
        var hiddenViewModeGrid = $('.product-viewmode .grid.hidden');

        // If we have less than 2 grid options - do nothing.
        if (gridViewModeSelector.length < 2) {

            return;
        }

        // Check if grid is selected by default
        if (hiddenViewModeGrid.hasClass('selected') || gridViewModeSelector.hasClass('selected')) {

            var cookieValue = cookieHelper.getCookie('venture_catalog_grid_viewmode_items') || 3;

            gridViewModeSelector.removeClass('selected').filter('.items-' + cookieValue).addClass('selected');
        } else {

            // Only when the list view mode is not selected, we need to select the first grid dropdown
            if (!listViewModeSelector.hasClass('selected')) {

                gridViewModeSelector.first().addClass('selected');
            }
        }

        gridViewModeSelector.on('click.venture', function (e) {

            // Do nothing if the selected view mode is already selected.
            if ($(this).hasClass('selected')) {

                return false;
            }

            // If there is no selected grid view mode, we need to trigger a click event on the hidden element.
            if (!gridViewModeSelector.hasClass("selected")) {

                hiddenViewModeGrid[0].click();
            }

            var items = parseInt($(this).attr('data-items'));

            cookieHelper.setCookie('venture_catalog_grid_viewmode_items', items);

            // Change the class on center-2 element.
            $('.center-2').removeClass('items-3 items-4').addClass('items-' + items);

            // Switch selected grid view modes.
            gridViewModeSelector.removeClass('selected').filter('.items-' + items).addClass('selected');
        });

        listViewModeSelector.on('click.venture', function (e) {

            gridViewModeSelector.removeClass('selected');
        });
    }

    function handleAjaxFiltersPriceRangeHandels() {

        function clonePriceInHandle(handle, priceElement) {

            var priceElementText = priceElement.text();

            handle.html($('<span/>').html(priceElementText));
        }

        function cloneMinMaxPrices(isRtl) {

            var minPriceSelector = !isRtl ? $('.currentMinPrice') : $('.currentMaxPrice');
            var maxPriceSelector = !isRtl ? $('.currentMaxPrice') : $('.currentMinPrice');

            clonePriceInHandle($('.ui-slider-handle').eq(0), minPriceSelector);
            clonePriceInHandle($('.ui-slider-handle').eq(1), maxPriceSelector);

            minPriceSelector.hide();
            maxPriceSelector.hide();
        }

        if ($("#slider").length > 0) {

            var isRtl = $('.nopAjaxFilters7Spikes').attr('data-isRTL') === 'true';

            var windowDelay;

            if ($('.ui-slider-handle').length === 2) {

                // Clone prices on initial load
                cloneMinMaxPrices(isRtl);

                // Clone prices after filtration. This is needed when a url with predefined "from" or "to" is opened.
                $(document).on('nopAjaxFiltersFiltrationCompleteEvent', function () {

                    cloneMinMaxPrices(isRtl);
                });

                $("#slider").on("slide", function (event, ui) {

                    clearTimeout(windowDelay);

                    windowDelay = window.setTimeout(function () {

                        var minPriceSelector = !isRtl ? $('.currentMinPrice') : $('.currentMaxPrice');
                        var maxPriceSelector = !isRtl ? $('.currentMaxPrice') : $('.currentMinPrice');

                        var element = $('#slider').find(ui.handle).index() === 1 ? minPriceSelector : maxPriceSelector;

                        clonePriceInHandle($(ui.handle), element);
                    }, 10);
                });
            }
        }
    }

    function handleHomePageVideoPlayer() {
        
        function initYoutubePlayer() {
            player = new YT.Player('video-iframe', {
                events: {
                    'onStateChange': function (state) {
                        if (state.data === 0) {

                            isPlayerRunning = false;

                            player.destroy();
                        }
                    }
                }
            });
        }

        function initVimeoPlayer(startTime) {
            player = new Vimeo.Player('video-iframe');

            player.setVolume(0);

            if (startTime && startTime > 0) {

                player.setCurrentTime(startTime);
            }

            player.on('ended', function () {

                isPlayerRunning = false;

                iFrame.remove();
            });
        }

        var playerElement = $('#video-player');

        if (playerElement.length === 0) {

            return;
        }

        var platformUrl = playerElement.attr('data-platform-url');
        var videoId = playerElement.attr('data-id');
        var startTime = playerElement.attr('data-start-time');

        var isPlayerRunning = false;
        var player;
        var iFrame;
        var videoSettings;

        if (platformUrl.indexOf('youtube') !== -1) {

            // Embed video options
            videoSettings = {
                autoplay: 1,
                controls: 0,
                rel: 0,
                showinfo: 0,
                iv_load_policy: 3,
                mute: 1,
                enablejsapi: 1,
                start: startTime
            };

        } else if (platformUrl.indexOf('vimeo') !== -1) {

            // Embed video options
            videoSettings = {
                autoplay: 1,
                byline: 0,
                portrait: 0,
                title: 0,
                background: 1
            };
        }

        playerElement.on('click', function () {

            if (isPlayerRunning) {

                return;
            }

            isPlayerRunning = true;

            iFrame = $('<iframe>', {
                src: platformUrl + videoId + '?' + $.param(videoSettings),
                id: 'video-iframe',
                frameborder: 0,
                allowfullscreen: 1
            });

            iFrame.appendTo(playerElement).show('slow');

            if (platformUrl.indexOf('youtube') !== -1) {

                initYoutubePlayer();

            } else if (platformUrl.indexOf('vimeo') !== -1) {
                
                initVimeoPlayer(startTime);
            }
        });
    }

})(jQuery, cookieHelper);