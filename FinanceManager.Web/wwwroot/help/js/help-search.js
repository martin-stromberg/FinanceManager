/**
 * Help Pages Search & Navigation
 * Handles loading, searching and displaying help documentation.
 */

class HelpPageManager {
    constructor() {
        this.searchIndex = null;
        this.language = this.detectLanguage();
        this.features = [];
        this.featureIdPattern = /^[a-z][a-z0-9-]{0,63}$/;
        console.log('[HelpPageManager] Initialized with language:', this.language);
        this.init();
    }

    /**
     * Detects the current language from the document or browser.
     */
    detectLanguage() {
        const blazorLang = document.documentElement.getAttribute('data-culture');
        if (blazorLang) {
            const result = blazorLang.startsWith('en') ? 'en' : 'de';
            console.log('[HelpPageManager] Detected language from data-culture:', result);
            return result;
        }

        const htmlLang = document.documentElement.lang;
        if (htmlLang) {
            const result = htmlLang.startsWith('en') ? 'en' : 'de';
            console.log('[HelpPageManager] Detected language from html.lang:', result);
            return result;
        }

        const navLang = navigator.language || navigator.userLanguage || 'de';
        const result = navLang.startsWith('en') ? 'en' : 'de';
        console.log('[HelpPageManager] Detected language from navigator:', result);
        return result;
    }

    /**
     * Initialize the help page.
     */
    async init() {
        try {
            console.log('[HelpPageManager] Initializing...');
            await this.loadSearchIndex();
            this.setupSearch();
            this.displayAllFeatures();
            console.log('[HelpPageManager] Initialization complete');
        } catch (error) {
            console.error('[HelpPageManager] Error initializing:', error);
            this.showError(error.message);
        }
    }

    /**
     * Show error message to user.
     */
    showError(message) {
        const featureListDiv = document.getElementById('featureList');
        if (!featureListDiv) {
            return;
        }

        const alert = document.createElement('div');
        alert.className = 'alert alert-danger';

        const strong = document.createElement('strong');
        strong.textContent = this.language === 'en' ? 'Error:' : 'Fehler:';
        alert.append(strong, document.createTextNode(` ${message}`), document.createElement('br'));

        const small = document.createElement('small');
        small.textContent = this.language === 'en'
            ? 'Please reload the page.'
            : 'Bitte versuchen Sie die Seite neu zu laden.';
        alert.appendChild(small);

        featureListDiv.replaceChildren(alert);
    }

    /**
     * Load the search index JSON for current language.
     */
    async loadSearchIndex() {
        try {
            const url = `/api/help/search-index/${this.language}.json`;
            console.log('[HelpPageManager] Loading search index from:', url);

            const response = await fetch(url);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: Failed to load search index`);
            }

            const data = await response.json();
            console.log('[HelpPageManager] Search index loaded:', data);

            this.searchIndex = data;
            this.features = Array.isArray(data.documents)
                ? data.documents.filter(f => this.isValidFeature(f))
                : [];
            console.log('[HelpPageManager] Loaded', this.features.length, 'documents');
        } catch (error) {
            console.error('[HelpPageManager] Error loading search index:', error);
            this.features = [];
            throw error;
        }
    }

    /**
     * Setup search input and button handlers.
     */
    setupSearch() {
        const searchInput = document.getElementById('helpSearch');
        const searchBtn = document.getElementById('searchBtn');

        if (!searchInput) {
            console.warn('[HelpPageManager] helpSearch input not found');
            return;
        }
        if (!searchBtn) {
            console.warn('[HelpPageManager] searchBtn not found');
            return;
        }

        console.log('[HelpPageManager] Setting up search handlers');

        searchInput.addEventListener('keyup', (e) => {
            if (e.key === 'Enter') {
                console.log('[HelpPageManager] Enter pressed, searching for:', searchInput.value);
                this.performSearch(searchInput.value);
            }
        });

        searchBtn.addEventListener('click', () => {
            console.log('[HelpPageManager] Search button clicked, searching for:', searchInput.value);
            this.performSearch(searchInput.value);
        });

        let debounceTimer;
        searchInput.addEventListener('input', (e) => {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(() => {
                if (e.target.value.length >= 2) {
                    console.log('[HelpPageManager] Auto-complete triggered for:', e.target.value);
                    this.showAutoComplete(e.target.value);
                }
            }, 300);
        });
    }

    /**
     * Show auto-complete suggestions (top 3).
     */
    showAutoComplete(query) {
        const results = this.searchFeatures(query).slice(0, 3);
        console.log('[HelpPageManager] Auto-complete results:', results);
    }

    /**
     * Perform full search.
     */
    performSearch(query) {
        const searchResultsDiv = document.getElementById('searchResults');
        const featureListDiv = document.getElementById('featureList');

        if (!searchResultsDiv || !featureListDiv) {
            return;
        }

        if (!query || query.length < 2) {
            console.log('[HelpPageManager] Search query too short, showing all features');
            searchResultsDiv.classList.add('d-none');
            featureListDiv.classList.remove('d-none');
            return;
        }

        const results = this.searchFeatures(query);
        console.log('[HelpPageManager] Search found', results.length, 'results for:', query);

        if (results.length === 0) {
            searchResultsDiv.replaceChildren(this.createAlert(
                'alert alert-warning',
                this.language === 'en' ? 'No results found' : 'Keine Ergebnisse gefunden'));
        } else {
            searchResultsDiv.replaceChildren(this.renderResults(results));
        }

        featureListDiv.classList.add('d-none');
        searchResultsDiv.classList.remove('d-none');
    }

    /**
     * Search features by query.
     */
    searchFeatures(query) {
        if (!this.features || this.features.length === 0) {
            console.warn('[HelpPageManager] No features loaded');
            return [];
        }

        const q = query.toLowerCase();
        return this.features.filter(f =>
            f.title.toLowerCase().includes(q) ||
            f.excerpt.toLowerCase().includes(q) ||
            f.keywords.some(k => k.toLowerCase().includes(q)));
    }

    /**
     * Display all features (hub view).
     */
    displayAllFeatures() {
        const featureListDiv = document.getElementById('featureList');
        if (!featureListDiv) {
            console.warn('[HelpPageManager] featureList div not found');
            return;
        }

        console.log('[HelpPageManager] Displaying', this.features.length, 'features');

        if (this.features.length === 0) {
            featureListDiv.replaceChildren(this.createAlert(
                'alert alert-warning',
                this.language === 'en' ? 'No documentation available' : 'Keine Dokumentation verfügbar'));
            return;
        }

        featureListDiv.replaceChildren(this.renderResults(this.features));
    }

    /**
     * Render feature results as cards.
     */
    renderResults(features) {
        const row = document.createElement('div');
        row.className = 'row';

        for (const feature of features) {
            row.appendChild(this.createFeatureCard(feature));
        }

        return row;
    }

    createFeatureCard(feature) {
        const column = document.createElement('div');
        column.className = 'col-md-6 col-lg-4 mb-3';

        const card = document.createElement('div');
        card.className = 'card feature-card feature-card-action h-100';
        card.tabIndex = 0;
        card.setAttribute('role', 'button');
        card.addEventListener('click', () => this.openFeature(this.language, feature.id));
        card.addEventListener('keydown', event => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                this.openFeature(this.language, feature.id);
            }
        });

        const body = document.createElement('div');
        body.className = 'card-body';

        const title = document.createElement('h5');
        title.className = 'card-title';
        title.textContent = feature.title;

        const excerpt = document.createElement('p');
        excerpt.className = 'card-text text-muted small';
        excerpt.textContent = feature.excerpt;

        body.append(title, excerpt);

        const footer = document.createElement('div');
        footer.className = 'card-footer bg-transparent border-top-0';

        const footerText = document.createElement('small');
        footerText.className = 'text-primary';
        footerText.textContent = this.language === 'en' ? 'Read more' : 'Mehr erfahren';
        footer.prepend(document.createTextNode('→ '));
        footer.appendChild(footerText);

        card.append(body, footer);
        column.appendChild(card);
        return column;
    }

    createAlert(className, message) {
        const alert = document.createElement('div');
        alert.className = className;
        alert.textContent = message;
        return alert;
    }

    isValidFeature(feature) {
        return feature
            && typeof feature.id === 'string'
            && this.featureIdPattern.test(feature.id)
            && typeof feature.title === 'string'
            && typeof feature.excerpt === 'string'
            && Array.isArray(feature.keywords)
            && feature.keywords.every(k => typeof k === 'string');
    }

    /**
     * Navigate to feature documentation (via Blazor route).
     */
    openFeature(language, featureId) {
        const normalizedFeatureId = typeof featureId === 'string'
            ? featureId.toLowerCase()
            : '';
        if (!this.featureIdPattern.test(normalizedFeatureId)) {
            console.warn('[HelpPageManager] Blocked invalid feature id:', featureId);
            return;
        }

        console.log('[HelpPageManager] Opening feature:', language, normalizedFeatureId);
        window.location.assign(`/help/view/${encodeURIComponent(normalizedFeatureId)}`);
    }
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        console.log('[HelpPageManager] DOM loaded, creating manager');
        window.helpPageManager = new HelpPageManager();
    });
} else {
    console.log('[HelpPageManager] DOM already loaded, creating manager');
    window.helpPageManager = new HelpPageManager();
}
