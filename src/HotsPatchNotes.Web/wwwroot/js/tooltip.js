// Tooltip manager for ability/talent references in patch content
window.patchTooltips = {
    heroData: null,
    currentTooltip: null,

    initialize: function(heroAbilities, heroTalents) {
        this.heroData = {
            abilities: heroAbilities,
            talents: heroTalents
        };
        this.setupTooltips();
    },

    setupTooltips: function() {
        // Find all text nodes in patch content
        const contentElements = document.querySelectorAll('.patch-section-content');

        contentElements.forEach(element => {
            // First, process ability references with hotkeys: "AbilityName [Q]"
            const abilityPattern = /\b([A-Z][^[\]]*?)\s*\[([QWERDZ]\d?)\]/g;
            this.processAbilityNodes(element, abilityPattern);

            // Then, process talent names (plain text matches)
            this.processTalentNodes(element);
        });
    },

    processAbilityNodes: function(element, pattern) {
        const walker = document.createTreeWalker(
            element,
            NodeFilter.SHOW_TEXT,
            null,
            false
        );

        const nodesToReplace = [];
        let node;

        while (node = walker.nextNode()) {
            // Skip if already inside a tooltip span (avoid double-wrapping)
            if (this.isInsideTooltipSpan(node)) {
                continue;
            }

            const matches = [...node.textContent.matchAll(pattern)];
            if (matches.length > 0) {
                nodesToReplace.push({ node, matches });
            }
        }

        nodesToReplace.forEach(({ node, matches }) => {
            this.replaceWithAbilityTooltips(node, matches);
        });
    },

    processTalentNodes: function(element) {
        if (!this.heroData || !this.heroData.talents || this.heroData.talents.length === 0) {
            return;
        }

        const walker = document.createTreeWalker(
            element,
            NodeFilter.SHOW_TEXT,
            null,
            false
        );

        const nodesToReplace = [];
        let node;

        while (node = walker.nextNode()) {
            // Skip if already inside a tooltip span
            if (this.isInsideTooltipSpan(node)) {
                continue;
            }

            const text = node.textContent;
            const matches = [];

            // Find all talent names in this text node
            this.heroData.talents.forEach(talent => {
                // Create a regex for exact talent name match with word boundaries
                const escapedName = talent.name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
                const talentRegex = new RegExp(`\\b${escapedName}\\b`, 'gi');
                let match;

                while ((match = talentRegex.exec(text)) !== null) {
                    matches.push({
                        index: match.index,
                        length: match[0].length,
                        talent: talent
                    });
                }
            });

            if (matches.length > 0) {
                // Sort by index to process in order
                matches.sort((a, b) => a.index - b.index);
                nodesToReplace.push({ node, talentMatches: matches });
            }
        }

        nodesToReplace.forEach(({ node, talentMatches }) => {
            this.replaceWithTalentTooltips(node, talentMatches);
        });
    },

    isInsideTooltipSpan: function(node) {
        let parent = node.parentNode;
        while (parent) {
            if (parent.classList &&
                (parent.classList.contains('ability-ref') || parent.classList.contains('talent-ref'))) {
                return true;
            }
            if (parent.classList && parent.classList.contains('patch-section-content')) {
                break;
            }
            parent = parent.parentNode;
        }
        return false;
    },

    replaceWithAbilityTooltips: function(textNode, matches) {
        const parent = textNode.parentNode;
        if (!parent) return;

        const text = textNode.textContent;
        const fragment = document.createDocumentFragment();

        let lastIndex = 0;
        matches.forEach(match => {
            // Add text before match
            if (match.index > lastIndex) {
                fragment.appendChild(
                    document.createTextNode(text.slice(lastIndex, match.index))
                );
            }

            // Create tooltip span
            const span = document.createElement('span');
            span.className = 'ability-ref';
            span.textContent = match[0];
            span.dataset.abilityName = match[1].trim();
            span.dataset.hotkey = match[2];
            span.dataset.type = 'ability';

            // Add hover listeners
            span.addEventListener('mouseenter', (e) => this.showTooltip(e, span));
            span.addEventListener('mouseleave', () => this.hideTooltip());

            fragment.appendChild(span);
            lastIndex = match.index + match[0].length;
        });

        // Add remaining text
        if (lastIndex < text.length) {
            fragment.appendChild(document.createTextNode(text.slice(lastIndex)));
        }

        parent.replaceChild(fragment, textNode);
    },

    replaceWithTalentTooltips: function(textNode, talentMatches) {
        const parent = textNode.parentNode;
        if (!parent) return;

        const text = textNode.textContent;
        const fragment = document.createDocumentFragment();

        let lastIndex = 0;
        talentMatches.forEach(match => {
            // Add text before match
            if (match.index > lastIndex) {
                fragment.appendChild(
                    document.createTextNode(text.slice(lastIndex, match.index))
                );
            }

            // Create tooltip span
            const span = document.createElement('span');
            span.className = 'talent-ref';
            span.textContent = text.slice(match.index, match.index + match.length);
            span.dataset.talentName = match.talent.name;
            span.dataset.type = 'talent';

            // Store talent data directly
            span._talentData = match.talent;

            // Add hover listeners
            span.addEventListener('mouseenter', (e) => this.showTooltip(e, span));
            span.addEventListener('mouseleave', () => this.hideTooltip());

            fragment.appendChild(span);
            lastIndex = match.index + match.length;
        });

        // Add remaining text
        if (lastIndex < text.length) {
            fragment.appendChild(document.createTextNode(text.slice(lastIndex)));
        }

        parent.replaceChild(fragment, textNode);
    },

    showTooltip: function(event, element) {
        const type = element.dataset.type;
        let tooltipHtml = '';

        if (type === 'ability') {
            const abilityName = element.dataset.abilityName;
            const hotkey = element.dataset.hotkey;

            if (!this.heroData || !this.heroData.abilities) return;

            const ability = this.heroData.abilities.find(
                a => a.name === abilityName || a.hotkey === hotkey
            );

            if (!ability) return;
            tooltipHtml = this.renderAbilityTooltip(ability);
        } else if (type === 'talent') {
            const talent = element._talentData;
            if (!talent) return;
            tooltipHtml = this.renderTalentTooltip(talent);
        } else {
            return;
        }

        // Create tooltip element
        const tooltip = document.createElement('div');
        tooltip.className = 'tooltip-popup';
        tooltip.innerHTML = tooltipHtml;

        // Position tooltip
        const rect = element.getBoundingClientRect();
        tooltip.style.left = rect.left + 'px';
        tooltip.style.top = (rect.bottom + 5) + 'px';

        document.body.appendChild(tooltip);
        this.currentTooltip = tooltip;

        // Adjust position if tooltip goes off screen
        setTimeout(() => {
            const tooltipRect = tooltip.getBoundingClientRect();
            if (tooltipRect.right > window.innerWidth) {
                tooltip.style.left = (window.innerWidth - tooltipRect.width - 10) + 'px';
            }
            if (tooltipRect.bottom > window.innerHeight) {
                tooltip.style.top = (rect.top - tooltipRect.height - 5) + 'px';
            }
        }, 10);
    },

    hideTooltip: function() {
        if (this.currentTooltip) {
            this.currentTooltip.remove();
            this.currentTooltip = null;
        }
    },

    renderAbilityTooltip: function(ability) {
        const iconUrl = ability.icon
            ? `https://raw.githubusercontent.com/heroespatchnotes/heroes-talents/master/images/talents/${ability.icon}`
            : '';

        const hotkeyHtml = ability.hotkey
            ? `<span class="tooltip-hotkey">[${ability.hotkey}]</span>`
            : '';

        const cooldownHtml = ability.cooldown
            ? `<span>⏱ ${ability.cooldown} s</span>`
            : '';

        const manaCostHtml = ability.manaCost
            ? `<span>💧 ${ability.manaCost}</span>`
            : '';

        const metaHtml = (cooldownHtml || manaCostHtml)
            ? `<div class="tooltip-meta">${cooldownHtml}${manaCostHtml}</div>`
            : '';

        return `
            <div class="ability-tooltip">
                <div class="tooltip-header">
                    ${iconUrl ? `<img src="${iconUrl}" alt="${ability.name}" class="tooltip-icon" onerror="this.style.display='none'" />` : ''}
                    <div class="tooltip-title">
                        <strong>${ability.name}</strong>
                        ${hotkeyHtml}
                    </div>
                </div>
                ${ability.description ? `<p class="tooltip-desc">${ability.description}</p>` : ''}
                ${metaHtml}
            </div>
        `;
    },

    renderTalentTooltip: function(talent) {
        const iconUrl = talent.icon
            ? `https://raw.githubusercontent.com/heroespatchnotes/heroes-talents/master/images/talents/${talent.icon}`
            : '';

        return `
            <div class="talent-tooltip">
                <div class="tooltip-header">
                    ${iconUrl ? `<img src="${iconUrl}" alt="${talent.name}" class="tooltip-icon" onerror="this.style.display='none'" />` : ''}
                    <div class="tooltip-title">
                        <strong>${talent.name}</strong>
                    </div>
                </div>
                ${talent.description ? `<p class="tooltip-desc">${talent.description}</p>` : ''}
            </div>
        `;
    }
};
