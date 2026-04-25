/**
 * Help Pages Search & Navigation
 * Handles loading, searching and displaying help documentation
 */

class HelpPageManager {
    constructor() {
        this.searchIndex = null;
        this.language = this.detectLanguage();
        this.features = [];
        console.log('[HelpPageManager] Initialized with language:', this.language);
        this.init();
    }

    /**
     * Detects the current language from the document or browser
     */
    detectLanguage() {
        // Priority 1: Check data attribute set by Blazor
        const blazorLang = document.documentElement.getAttribute('data-culture');
        if (blazorLang) {
            const result = blazorLang.startsWith('en') ? 'en' : 'de';
            console.log('[HelpPageManager] Detected language from data-culture:', result);
            return result;
        }

        // Priority 2: Check html lang attribute
        const html = document.documentElement;
        const htmlLang = html.lang;
        if (htmlLang) {
            const result = htmlLang.startsWith('en') ? 'en' : 'de';
            console.log('[HelpPageManager] Detected language from html.lang:', result);
            return result;
        }

        // Priority 3: Check browser language
        const navLang = navigator.language || navigator.userLanguage;
        const result = navLang.startsWith('en') ? 'en' : 'de';
        console.log('[HelpPageManager] Detected language from navigator:', result);
        return result;
    }

    /**
     * Initialize the help page
     */
    async init() {
        try {
            console.log('[HelpPageManager] Initializing...');

            // Load search index
            await this.loadSearchIndex();

            // Setup search functionality
            this.setupSearch();

            // Display all features
            this.displayAllFeatures();

            console.log('[HelpPageManager] Initialization complete');
        } catch (error) {
            console.error('[HelpPageManager] Error initializing:', error);
            this.showError(error.message);
        }
    }

    /**
     * Show error message to user
     */
    showError(message) {
        const featureListDiv = document.getElementById('featureList');
        if (featureListDiv) {
            featureListDiv.innerHTML = `
                <div class="alert alert-danger">
                    <strong>Fehler:</strong> ${message}<br>
                    <small>Bitte versuchen Sie die Seite neu zu laden.</small>
                </div>
            `;
        }
    }

    /**
     * Load the search index JSON for current language
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
            this.features = data.documents || [];
            console.log('[HelpPageManager] Loaded', this.features.length, 'documents');
        } catch (error) {
            console.error('[HelpPageManager] Error loading search index:', error);
            this.features = [];
            throw error;
        }
    }

    /**
     * Setup search input and button handlers
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

        // Search on Enter
        searchInput.addEventListener('keyup', (e) => {
            if (e.key === 'Enter') {
                console.log('[HelpPageManager] Enter pressed, searching for:', searchInput.value);
                this.performSearch(searchInput.value);
            }
        });

        // Search on button click
        searchBtn.addEventListener('click', () => {
            console.log('[HelpPageManager] Search button clicked, searching for:', searchInput.value);
            this.performSearch(searchInput.value);
        });

        // Auto-complete on typing (debounced)
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
     * Show auto-complete suggestions (top 3)
     */
    showAutoComplete(query) {
        const results = this.searchFeatures(query).slice(0, 3);
        console.log('[HelpPageManager] Auto-complete results:', results);

        if (results.length === 0) return;

        // TODO: Implement auto-complete UI
    }

    /**
     * Perform full search
     */
    performSearch(query) {
        const searchResultsDiv = document.getElementById('searchResults');
        const featureListDiv = document.getElementById('featureList');

        if (!query || query.length < 2) {
            console.log('[HelpPageManager] Search query too short, showing all features');
            searchResultsDiv.classList.add('d-none');
            featureListDiv.classList.remove('d-none');
            return;
        }

        const results = this.searchFeatures(query);
        console.log('[HelpPageManager] Search found', results.length, 'results for:', query);

        if (results.length === 0) {
            searchResultsDiv.innerHTML = `
                <div class="alert alert-warning">
                    ${this.language === 'en' ? 'No results found' : 'Keine Ergebnisse gefunden'}
                </div>
            `;
        } else {
            searchResultsDiv.innerHTML = this.renderResults(results);
        }

        featureListDiv.classList.add('d-none');
        searchResultsDiv.classList.remove('d-none');
    }

    /**
     * Search features by query
     */
    searchFeatures(query) {
        if (!this.features || this.features.length === 0) {
            console.warn('[HelpPageManager] No features loaded');
            return [];
        }

        const q = query.toLowerCase();
        return this.features.filter(f => 
            (f.title && f.title.toLowerCase().includes(q)) ||
            (f.excerpt && f.excerpt.toLowerCase().includes(q)) ||
            (f.keywords && f.keywords.some(k => k.toLowerCase().includes(q)))
        );
    }

    /**
     * Display all features (hub view)
     */
    displayAllFeatures() {
        const featureListDiv = document.getElementById('featureList');
        if (!featureListDiv) {
            console.warn('[HelpPageManager] featureList div not found');
            return;
        }

        console.log('[HelpPageManager] Displaying', this.features.length, 'features');

        if (this.features.length === 0) {
            featureListDiv.innerHTML = `
                <div class="alert alert-warning">
                    ${this.language === 'en' ? 'No documentation available' : 'Keine Dokumentation verfügbar'}
                </div>
            `;
            return;
        }

        featureListDiv.innerHTML = this.renderResults(this.features);
    }

    /**
     * Render feature results as cards
     */
    renderResults(features) {
        const manager = this;
        return `
            <div class="row">
                ${features.map(f => `
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="card feature-card h-100" style="cursor: pointer;" onclick="window.helpPageManager.openFeature('${manager.language}', '${f.id}')">
                            <div class="card-body">
                                <h5 class="card-title">${f.title}</h5>
                                <p class="card-text text-muted small">${f.excerpt}</p>
                            </div>
                            <div class="card-footer bg-transparent border-top-0">
                                <small class="text-primary">→ ${manager.language === 'en' ? 'Read more' : 'Mehr erfahren'}</small>
                            </div>
                        </div>
                    </div>
                `).join('')}
            </div>
        `;
    }

    /**
     * Navigate to feature documentation (via Blazor route)
     */
    openFeature(language, featureId) {
        console.log('[HelpPageManager] Opening feature:', language, featureId);
        // Navigate to Blazor route with full app layout
        window.location.href = `/help/view/${featureId.toLowerCase()}`;
    }
}

// Initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        console.log('[HelpPageManager] DOM loaded, creating manager');
        window.helpPageManager = new HelpPageManager();
    });
} else {
    console.log('[HelpPageManager] DOM already loaded, creating manager');
    window.helpPageManager = new HelpPageManager();
}

