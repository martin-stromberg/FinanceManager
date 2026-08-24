/**
 * Search behavior for the user-facing help hub.
 */

class HelpPageManager {
    constructor() {
        this.language = this.detectLanguage();
        this.features = [];
        this.featureIdPattern = /^[a-z][a-z0-9-]{0,63}$/;
        this.init();
    }

    detectLanguage() {
        const culture = document.documentElement.getAttribute('data-culture') || document.documentElement.lang || navigator.language || 'de';
        return culture.startsWith('en') ? 'en' : 'de';
    }

    async init() {
        try {
            await this.loadSearchIndex();
            this.setupSearch();
        } catch {
            this.showError(this.language === 'en'
                ? 'The help search is currently unavailable.'
                : 'Die Hilfesuche ist aktuell nicht verfuegbar.');
        }
    }

    async loadSearchIndex() {
        const response = await fetch(`/api/help/search-index/${this.language}.json`);
        if (!response.ok) {
            throw new Error('Search index unavailable');
        }

        const data = await response.json();
        this.features = Array.isArray(data.documents)
            ? data.documents.filter(feature => this.isValidFeature(feature))
            : [];
    }

    setupSearch() {
        const searchInput = document.getElementById('helpSearch');
        const searchBtn = document.getElementById('searchBtn');
        if (!searchInput || !searchBtn) {
            return;
        }

        searchInput.addEventListener('keyup', event => {
            if (event.key === 'Enter') {
                this.performSearch(searchInput.value);
            }
        });

        searchBtn.addEventListener('click', () => this.performSearch(searchInput.value));

        let debounceTimer;
        searchInput.addEventListener('input', event => {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(() => this.performSearch(event.target.value), 200);
        });
    }

    performSearch(query) {
        const searchResultsDiv = document.getElementById('searchResults');
        const featureListDiv = document.getElementById('featureList');
        if (!searchResultsDiv || !featureListDiv) {
            return;
        }

        const normalizedQuery = (query || '').trim();
        if (normalizedQuery.length < 2) {
            searchResultsDiv.classList.add('d-none');
            featureListDiv.classList.remove('d-none');
            searchResultsDiv.replaceChildren();
            return;
        }

        const results = this.searchFeatures(normalizedQuery);
        searchResultsDiv.replaceChildren(results.length === 0
            ? this.createAlert('alert alert-warning', this.language === 'en' ? 'No matching help topics found.' : 'Keine passenden Hilfethemen gefunden.')
            : this.renderResults(results));

        featureListDiv.classList.add('d-none');
        searchResultsDiv.classList.remove('d-none');
    }

    searchFeatures(query) {
        const q = query.toLowerCase();
        return this.features.filter(feature =>
            feature.title.toLowerCase().includes(q) ||
            feature.excerpt.toLowerCase().includes(q) ||
            feature.keywords.some(keyword => keyword.toLowerCase().includes(q)));
    }

    renderResults(features) {
        const grid = document.createElement('div');
        grid.className = 'help-topic-grid';

        for (const feature of features) {
            grid.appendChild(this.createFeatureCard(feature));
        }

        return grid;
    }

    createFeatureCard(feature) {
        const card = document.createElement('a');
        card.className = 'help-topic-card';
        card.href = `/help/view/${encodeURIComponent(feature.id)}`;

        const label = document.createElement('span');
        label.className = 'help-topic-card-label';
        label.textContent = this.language === 'en' ? 'Guide' : 'Anleitung';

        const title = document.createElement('h3');
        title.textContent = feature.title;

        const excerpt = document.createElement('p');
        excerpt.textContent = feature.excerpt;

        const action = document.createElement('span');
        action.className = 'help-topic-card-action';
        action.textContent = this.language === 'en' ? 'Open topic' : 'Thema oeffnen';

        card.append(label, title, excerpt, action);
        return card;
    }

    createAlert(className, message) {
        const alert = document.createElement('div');
        alert.className = className;
        alert.textContent = message;
        return alert;
    }

    showError(message) {
        const searchResultsDiv = document.getElementById('searchResults');
        if (!searchResultsDiv) {
            return;
        }

        searchResultsDiv.replaceChildren(this.createAlert('alert alert-warning', message));
        searchResultsDiv.classList.remove('d-none');
    }

    isValidFeature(feature) {
        return feature
            && typeof feature.id === 'string'
            && this.featureIdPattern.test(feature.id)
            && typeof feature.title === 'string'
            && typeof feature.excerpt === 'string'
            && Array.isArray(feature.keywords)
            && feature.keywords.every(keyword => typeof keyword === 'string');
    }
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.helpPageManager = new HelpPageManager();
    });
} else {
    window.helpPageManager = new HelpPageManager();
}
