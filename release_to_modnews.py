import os

import markdown
import requests

GITHUB_REPO = os.getenv("GITHUB_REPOSITORY", "Gurge44/EndlessHostRoles")
API_URL = f"https://api.github.com/repos/{GITHUB_REPO}/releases"
POST_URL = os.getenv("MODNEWS_API_URL")
POST_TOKEN = os.getenv("MODNEWS_API_TOKEN")


def fetch_latest_release():
    response = requests.get(API_URL)
    response.raise_for_status()
    releases = response.json()

    mod_news_list = []

    for i, release in enumerate(releases):
        tag = release["tag_name"]
        title = f"Endless Host Roles {tag}"
        subtitle = f"★★★★Release {tag}★★★★"
        short_title = f"★EHR {tag}"
        date = release["published_at"]
        body_md = release["body"]

        body_html = markdown.markdown(body_md)
        body_html = (
            body_html
            .replace("</p>", "</p>\n")
            .replace("<li>", " - ")
            .replace("</li>", "\n")
            .replace("<ul>", "")
            .replace("</ul>", "")
            .replace("<strong>", "<b>").replace("</strong>", "</b>")
        )

        intro_line = f"<size=150%>Welcome to EHR {tag}.</size>\n"
        subtitle_line = f"<size=125%>{subtitle}</size>\n\n"

        mod_news = {
            "Number": 100000 + i,
            "Title": title,
            "SubTitle": subtitle,
            "ShortTitle": short_title,
            "Text": intro_line + subtitle_line + body_html,
            "Date": date
        }

        mod_news_list.append(mod_news)

    return mod_news_list


def send_to_api(mod_news_data):
    headers = {"Content-Type": "application/json"}
    if POST_TOKEN:
        headers["Authorization"] = f"Bearer {POST_TOKEN}"
    response = requests.post(POST_URL, headers=headers, json=mod_news_data)
    response.raise_for_status()
    print("✅ ModNews posted successfully!")


if __name__ == "__main__":
    data = fetch_latest_release()
    send_to_api(data)
