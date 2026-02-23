/**
 * LiveSQL Flow Animation - JavaScript Interop
 * Handles SVG animation control for the flow diagram.
 */
window.liveSqlFlow = (function () {
    'use strict';

    const state = {};

    function getState(id) {
        if (!state[id]) {
            state[id] = { step: 0, paused: true, speed: 1.0, initialized: false, dotNetRef: null };
        }
        return state[id];
    }

    function getContainer(containerId) {
        return document.getElementById(containerId);
    }

    function getAllDots(container) {
        if (!container) return [];
        return Array.from(container.querySelectorAll('.flow-dot'));
    }

    function getAllNodes(container) {
        if (!container) return [];
        return Array.from(container.querySelectorAll('.flow-node'));
    }

    function getAllEdges(container) {
        if (!container) return [];
        return Array.from(container.querySelectorAll('.flow-edge'));
    }

    /**
     * Initialize diagram: hide all dots, set nodes to dimmed state.
     */
    function initDiagram(containerId) {
        const container = getContainer(containerId);
        if (!container) return;

        const s = getState(containerId);
        s.initialized = true;
        s.step = 0;
        s.paused = true;

        // Clear any pending completion timer
        if (s.completionTimer) {
            clearTimeout(s.completionTimer);
            s.completionTimer = null;
        }

        // Hide all animated dots initially
        getAllDots(container).forEach(dot => {
            dot.style.opacity = '0';
            const anim = dot.querySelector('animateMotion');
            if (anim && !anim.getAttribute('data-base-dur')) {
                anim.setAttribute('data-base-dur', anim.getAttribute('dur'));
            }
        });

        // Dim all nodes initially
        getAllNodes(container).forEach(node => {
            node.style.opacity = '0.3';
            node.style.transition = 'opacity 0.4s ease';
        });

        // Dim all edges
        getAllEdges(container).forEach(edge => {
            edge.style.opacity = '0.2';
            edge.style.transition = 'opacity 0.4s ease';
        });

        // Pause SVG animations
        const svg = container.querySelector('svg');
        if (svg && svg.pauseAnimations) {
            svg.pauseAnimations();
        }
    }

    return {
        /**
         * Initialize the flow diagram (called after render).
         */
        initFlowDiagram: function (containerId) {
            initDiagram(containerId);
        },

        /**
         * Play full animation: reveal nodes bottom-to-top with animated dots.
         * dotNetRef is a .NET object reference for callback when animation completes.
         */
        playAnimation: function (containerId, speed, dotNetRef) {
            const container = getContainer(containerId);
            if (!container) return;

            const s = getState(containerId);
            s.paused = false;
            s.speed = speed || 1.0;
            if (dotNetRef) s.dotNetRef = dotNetRef;

            // Reset everything first
            initDiagram(containerId);
            s.paused = false;

            const nodes = getAllNodes(container);
            const edges = getAllEdges(container);
            const dots = getAllDots(container);
            const svg = container.querySelector('svg');

            const baseDelay = 600 / s.speed;
            const totalNodes = nodes.length;

            // Reveal nodes bottom-to-top
            for (let i = 0; i < totalNodes; i++) {
                const nodeIndex = totalNodes - 1 - i;
                const delay = i * baseDelay;

                ((idx, d) => {
                    setTimeout(() => {
                        if (s.paused) return;
                        if (nodes[idx]) {
                            nodes[idx].style.opacity = '1';
                            const rect = nodes[idx].querySelector('rect');
                            if (rect) {
                                rect.setAttribute('stroke-width', '4');
                                setTimeout(() => {
                                    if (rect) rect.setAttribute('stroke-width', '2');
                                }, 300);
                            }
                        }
                        s.step = totalNodes - idx;
                    }, d);
                })(nodeIndex, delay);
            }

            // Reveal edges and dots
            const edgeStartDelay = baseDelay * 0.5;
            for (let i = 0; i < edges.length; i++) {
                ((idx, d) => {
                    setTimeout(() => {
                        if (s.paused) return;
                        if (edges[idx]) edges[idx].style.opacity = '1';
                        if (dots[idx]) dots[idx].style.opacity = '0.8';
                    }, d);
                })(i, edgeStartDelay + i * baseDelay * 0.7);
            }

            // Unpause SVG dot animations midway
            setTimeout(() => {
                if (s.paused) return;
                dots.forEach(dot => {
                    const anim = dot.querySelector('animateMotion');
                    if (anim) {
                        const baseDur = parseFloat(anim.getAttribute('data-base-dur') || '2');
                        const newDur = baseDur / Math.max(0.1, s.speed);
                        anim.setAttribute('dur', newDur + 's');
                    }
                });
                if (svg && svg.unpauseAnimations) {
                    svg.unpauseAnimations();
                }
            }, baseDelay * 2);

            // Calculate total animation duration and fire completion callback
            const totalDuration = (totalNodes * baseDelay) + 500;
            if (s.completionTimer) clearTimeout(s.completionTimer);
            s.completionTimer = setTimeout(() => {
                if (s.paused) return;
                s.paused = true;
                // Notify .NET that animation is complete
                if (s.dotNetRef) {
                    try {
                        s.dotNetRef.invokeMethodAsync('AnimationFinished');
                    } catch (e) { /* component may be disposed */ }
                }
            }, totalDuration);
        },

        /**
         * Pause all animations — freeze current state.
         */
        pauseAnimation: function (containerId) {
            const container = getContainer(containerId);
            if (!container) return;

            const s = getState(containerId);
            s.paused = true;

            // Cancel completion timer
            if (s.completionTimer) {
                clearTimeout(s.completionTimer);
                s.completionTimer = null;
            }

            const svg = container.querySelector('svg');
            if (svg && svg.pauseAnimations) {
                svg.pauseAnimations();
            }
        },

        /**
         * Step forward: reveal one more node (bottom-to-top).
         */
        stepForward: function (containerId) {
            const container = getContainer(containerId);
            if (!container) return;

            const s = getState(containerId);
            const nodes = getAllNodes(container);
            const edges = getAllEdges(container);
            const dots = getAllDots(container);

            if (!s.initialized) {
                initDiagram(containerId);
            }

            const currentStep = s.step;
            const revIndex = nodes.length - 1 - currentStep;

            if (revIndex >= 0 && revIndex < nodes.length) {
                nodes[revIndex].style.opacity = '1';
                const rect = nodes[revIndex].querySelector('rect');
                if (rect) {
                    rect.setAttribute('stroke-width', '4');
                    setTimeout(() => { rect.setAttribute('stroke-width', '2'); }, 500);
                }
                if (edges[revIndex]) edges[revIndex].style.opacity = '1';
                if (dots[revIndex]) dots[revIndex].style.opacity = '0.8';

                s.step = currentStep + 1;

                // Unpause SVG so dots move
                const svg = container.querySelector('svg');
                if (svg && svg.unpauseAnimations) svg.unpauseAnimations();
            }
        },

        /**
         * Step backward: hide the last revealed node.
         */
        stepBackward: function (containerId) {
            const container = getContainer(containerId);
            if (!container) return;

            const s = getState(containerId);
            const nodes = getAllNodes(container);
            const edges = getAllEdges(container);
            const dots = getAllDots(container);

            if (s.step > 0) {
                s.step--;
                const revIndex = nodes.length - 1 - s.step;
                if (revIndex >= 0 && revIndex < nodes.length) {
                    nodes[revIndex].style.opacity = '0.3';
                    if (edges[revIndex]) edges[revIndex].style.opacity = '0.2';
                    if (dots[revIndex]) dots[revIndex].style.opacity = '0';
                }
            }
        },

        /**
         * Show all nodes, edges and dots at full opacity.
         */
        showAll: function (containerId) {
            const container = getContainer(containerId);
            if (!container) return;

            const s = getState(containerId);

            // Cancel any pending completion
            if (s.completionTimer) {
                clearTimeout(s.completionTimer);
                s.completionTimer = null;
            }

            getAllNodes(container).forEach(node => {
                node.style.opacity = '1';
                node.style.transition = 'opacity 0.3s ease';
            });

            getAllEdges(container).forEach(edge => {
                edge.style.opacity = '1';
                edge.style.transition = 'opacity 0.3s ease';
            });

            getAllDots(container).forEach(dot => {
                dot.style.opacity = '0.8';
            });

            s.step = getAllNodes(container).length;

            const svg = container.querySelector('svg');
            if (svg && svg.unpauseAnimations) svg.unpauseAnimations();
        },

        /**
         * Highlight a specific node.
         */
        highlightNode: function (containerId, nodeId) {
            const container = getContainer(containerId);
            if (!container) return;

            getAllNodes(container).forEach((node, i) => {
                const rect = node.querySelector('rect');
                if (rect) {
                    rect.setAttribute('stroke-width', i === nodeId ? '4' : '2');
                }
            });
        }
    };
})();
