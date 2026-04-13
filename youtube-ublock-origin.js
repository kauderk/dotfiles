// ==UserScript==
// @name         youtube term blacklist
// @namespace    http://tampermonkey.net/
// @version      2025-06-16
// @description  Block YouTube videos based on keywords with a taggable pause/resume feature
// @author       You
// @match        https://www.youtube.com/*
// @icon         https://www.google.com/s2/favicons?sz=64&domain=tampermonkey.net
// @grant        GM_registerMenuCommand
// ==/UserScript==

(function () {
    'use strict'

    let is_paused = false
    window.blacklist_disposables = []
    const ITEM_SELECTOR = 'ytd-rich-item-renderer' /*, ytd-rich-grid-media, ytd-video-renderer*/

    function do_dispose() {
        for (let disposable of window.blacklist_disposables) {
            disposable()
        }
        window.blacklist_disposables = []
    }

    function init_filtering() {
        do_dispose()

        function is_index_page() {
            return (
                location.pathname.startsWith('/results') || 
                location.pathname.startsWith('/@')
            )
        }

        const terms = [
            'expedition 33',
        ]
        const blacklist = [];
        for (let term of terms) {
            const esc = term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
            blacklist.push(new RegExp(`\\b${esc}\\b`, 'i'))
        }

        const TITLE_SELECTOR = '#video-title'
        const CHANNEL_SELECTOR = 'ytd-channel-name a'

        function filter_item(item) {
            if (is_index_page()) return

            if (item.dataset.blacklist) return
            item.dataset.blacklist = 'seen'

            const linkEl = item.querySelector('a#video-title-link')
            const title = linkEl ? linkEl.getAttribute('aria-label').trim() : '‹no title›'

            const channelEl = item.querySelector(CHANNEL_SELECTOR)
            const channel = channelEl ? channelEl.textContent.trim() : ''

            for (const re of blacklist) {
                if (re.test(title) || re.test(channel)) {
                    console.log(
                        `Blocking video:\n` +
                        `    title:   "${title}"\n` +
                        `    channel: "${channel}"\n` +
                        `    matched:  ${re}`,
                        linkEl
                    )
                    item.style.display = 'none'
                    item.dataset.blacklist = 'blocked'
                    return
                }
            }
        }

        const observer = new MutationObserver(mutations => {
            for (const { addedNodes } of mutations) {
                for (const node of addedNodes) {
                    if (!(node instanceof HTMLElement)) { continue }

                    if (node.matches(ITEM_SELECTOR)) { filter_item(node) }
                    else node.querySelectorAll(ITEM_SELECTOR).forEach(filter_item)
                }
            }
        })
        observer.observe(document.body, { childList: true, subtree: true })
        window.blacklist_disposables.push(() => observer.disconnect())

        function scan_page() {
            if (is_index_page()) {
                console.log('will not filter')
                return
            }
            for (let item of document.querySelectorAll(ITEM_SELECTOR)) {
                // @reset_blacklist
                delete item.dataset.blacklist
                filter_item(item)
            }
        }

        window.addEventListener('yt-navigate-finish', scan_page)
        window.blacklist_disposables.push(() => window.removeEventListener('yt-navigate-finish', scan_page))

        scan_page()
    }

    GM_registerMenuCommand('Toggle Filter', () => {
        is_paused = !is_paused 
        if (is_paused) {
            do_dispose()
            for (const item of document.querySelectorAll(ITEM_SELECTOR)) {
                // @reset_blacklist
                item.style.display = ''
                delete item.dataset.blacklist
            }
            console.log('pause Filter')
        }
        else {
            init_filtering()
            console.log('resume Filter')
        }
    })

    if (!is_paused) {
        init_filtering()
    }
})()