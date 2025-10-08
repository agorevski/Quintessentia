// Enhanced site interactions and animations

// Add smooth scroll behavior
document.documentElement.style.scrollBehavior = 'smooth';

// Enhanced form input interactions
document.addEventListener('DOMContentLoaded', function() {
    // Add floating label effect
    const inputs = document.querySelectorAll('.form-control');
    inputs.forEach(input => {
        // Add focus animation class
        input.addEventListener('focus', function() {
            this.parentElement.classList.add('input-focused');
        });
        
        input.addEventListener('blur', function() {
            this.parentElement.classList.remove('input-focused');
        });
    });

    // Add ripple effect to buttons
    const buttons = document.querySelectorAll('.btn');
    buttons.forEach(button => {
        button.addEventListener('click', function(e) {
            const ripple = document.createElement('span');
            const rect = this.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height);
            const x = e.clientX - rect.left - size / 2;
            const y = e.clientY - rect.top - size / 2;
            
            ripple.style.width = ripple.style.height = size + 'px';
            ripple.style.left = x + 'px';
            ripple.style.top = y + 'px';
            ripple.classList.add('ripple');
            
            this.appendChild(ripple);
            
            setTimeout(() => {
                ripple.remove();
            }, 600);
        });
    });

    // Add stagger animation to cards
    const cards = document.querySelectorAll('.card');
    cards.forEach((card, index) => {
        card.style.animationDelay = `${index * 0.1}s`;
    });

    // Add parallax effect to background
    let ticking = false;
    window.addEventListener('scroll', function() {
        if (!ticking) {
            window.requestAnimationFrame(function() {
                const scrolled = window.pageYOffset;
                const parallaxElements = document.querySelectorAll('.container');
                
                parallaxElements.forEach(element => {
                    const speed = 0.5;
                    element.style.transform = `translateY(${scrolled * speed * 0.05}px)`;
                });
                
                ticking = false;
            });
            ticking = true;
        }
    });

    // Add intersection observer for fade-in animations
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver(function(entries) {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
            }
        });
    }, observerOptions);

    const elementsToObserve = document.querySelectorAll('.alert, .list-group-item, .badge');
    elementsToObserve.forEach(element => {
        observer.observe(element);
    });

    // Add smooth hover effect for icons
    const icons = document.querySelectorAll('.bi');
    icons.forEach(icon => {
        icon.addEventListener('mouseenter', function() {
            this.style.transform = 'scale(1.2) rotate(5deg)';
        });
        
        icon.addEventListener('mouseleave', function() {
            this.style.transform = 'scale(1) rotate(0deg)';
        });
    });

    // Add pulse animation to important elements on hover
    const importantElements = document.querySelectorAll('.btn-primary, .badge, .bi-stars');
    importantElements.forEach(element => {
        element.addEventListener('mouseenter', function() {
            this.style.animation = 'pulse 0.5s ease-in-out';
        });
        
        element.addEventListener('animationend', function() {
            this.style.animation = '';
        });
    });

    // Add typewriter effect to lead text (optional, can be enabled)
    // const leadText = document.querySelector('.lead');
    // if (leadText) {
    //     const text = leadText.textContent;
    //     leadText.textContent = '';
    //     let i = 0;
    //     const typeWriter = () => {
    //         if (i < text.length) {
    //             leadText.textContent += text.charAt(i);
    //             i++;
    //             setTimeout(typeWriter, 50);
    //         }
    //     };
    //     setTimeout(typeWriter, 500);
    // }

    // Enhanced progress bar animation
    const progressBars = document.querySelectorAll('.progress-bar');
    progressBars.forEach(bar => {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const width = bar.style.width;
                    bar.style.width = '0%';
                    setTimeout(() => {
                        bar.style.width = width;
                    }, 100);
                }
            });
        });
        observer.observe(bar);
    });

    // Add smooth transition for form validation
    const forms = document.querySelectorAll('form');
    forms.forEach(form => {
        form.addEventListener('submit', function(e) {
            const inputs = this.querySelectorAll('input[required]');
            inputs.forEach(input => {
                if (!input.validity.valid) {
                    input.style.animation = 'shake 0.5s';
                    setTimeout(() => {
                        input.style.animation = '';
                    }, 500);
                }
            });
        });
    });

    // Add smooth fade-in for dynamically added content
    const mutationObserver = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            mutation.addedNodes.forEach(function(node) {
                if (node.nodeType === 1) { // Element node
                    node.style.opacity = '0';
                    node.style.transform = 'translateY(10px)';
                    setTimeout(() => {
                        node.style.transition = 'all 0.4s ease-out';
                        node.style.opacity = '1';
                        node.style.transform = 'translateY(0)';
                    }, 10);
                }
            });
        });
    });

    // Observe status list for new items
    const statusList = document.getElementById('statusList');
    if (statusList) {
        mutationObserver.observe(statusList, { childList: true });
    }

    // Add keyboard navigation enhancements
    document.addEventListener('keydown', function(e) {
        // ESC key to close/cancel operations
        if (e.key === 'Escape') {
            const cancelButtons = document.querySelectorAll('[data-cancel]');
            if (cancelButtons.length > 0) {
                cancelButtons[0].click();
            }
        }
    });

    // Add loading state management
    window.showLoading = function(element) {
        element.classList.add('loading');
        element.style.pointerEvents = 'none';
        element.style.opacity = '0.6';
    };

    window.hideLoading = function(element) {
        element.classList.remove('loading');
        element.style.pointerEvents = 'auto';
        element.style.opacity = '1';
    };

    // Add toast notification system (can be used for future enhancements)
    window.showToast = function(message, type = 'info', duration = 3000) {
        const toast = document.createElement('div');
        toast.className = `toast-notification toast-${type}`;
        toast.textContent = message;
        toast.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            padding: 15px 25px;
            background: white;
            border-radius: 10px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
            z-index: 10000;
            animation: slideInRight 0.4s ease-out;
        `;
        
        document.body.appendChild(toast);
        
        setTimeout(() => {
            toast.style.animation = 'slideOutRight 0.4s ease-out';
            setTimeout(() => toast.remove(), 400);
        }, duration);
    };

    // Console easter egg
    console.log('%cQuintessentia ✨', 'font-size: 24px; font-weight: bold; color: #1DB954;');
    console.log('%cDistilling audio down to its pure essence', 'font-size: 14px; color: #667eea;');
    console.log('%cBuilt with ❤️ and modern Web 2.0 design', 'font-size: 12px; color: #999;');
});

// Add CSS animations for ripple effect
const style = document.createElement('style');
style.textContent = `
    .ripple {
        position: absolute;
        border-radius: 50%;
        background: rgba(255, 255, 255, 0.6);
        transform: scale(0);
        animation: ripple-animation 0.6s ease-out;
        pointer-events: none;
    }

    @keyframes ripple-animation {
        to {
            transform: scale(2);
            opacity: 0;
        }
    }

    @keyframes shake {
        0%, 100% { transform: translateX(0); }
        25% { transform: translateX(-10px); }
        75% { transform: translateX(10px); }
    }

    @keyframes slideInRight {
        from {
            transform: translateX(100%);
            opacity: 0;
        }
        to {
            transform: translateX(0);
            opacity: 1;
        }
    }

    @keyframes slideOutRight {
        from {
            transform: translateX(0);
            opacity: 1;
        }
        to {
            transform: translateX(100%);
            opacity: 0;
        }
    }

    .input-focused {
        transform: scale(1.02);
    }

    /* Smooth transitions for all interactive elements */
    .form-control,
    .btn,
    .card,
    .badge,
    .bi {
        transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    }
`;
document.head.appendChild(style);
