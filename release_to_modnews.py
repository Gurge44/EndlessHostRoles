import os
import re
from datetime import datetime

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
        subtitle = f"Update if you haven't already!"
        short_title = f"∞ EHR {tag}"
        date = release["published_at"]
        body_md = release["body"]

        body_html = markdown.markdown(body_md)
        body_html = (
            body_html
            .replace("&gt;", ">")
            .replace("&amp;", "&")
            .replace("<code>", "<mark=#00000033>")
            .replace("</code>", "</mark>")
            .replace("[!CAUTION]", "<b><u>CAUTION!</u></b>\n")
            .replace("[!WARNING]", "<b><u>WARNING!</u></b>\n")
            .replace("[!IMPORTANT]", "<b><u>IMPORTANT:</u></b>\n")
            .replace("[!NOTE]", "<b><u>Note:</u></b>\n")
            .replace("[!TIP]", "<b><u>Tip:</u></b>\n")
            .replace("<blockquote>", "\n<size=70%>")
            .replace("</blockquote>", "</size>\n")
            .replace("<em>", "<i>")
            .replace("</em>", "</i>")
            .replace("<p>", "\n")
            .replace("</p>", "\n")
            .replace("<li>", "  - ")
            .replace("</li>", "\n")
            .replace("<ul>", "")
            .replace("</ul>", "")
            .replace("<strong>", "<b>")
            .replace("</strong>", "</b>")
            .replace("<hr />", "---------------------------------------------------------------------------")
            .replace("<h6>", "<b>")
            .replace("</h6>", "</b>")
            .replace("<h5>", "<b>")
            .replace("</h5>", "</b>")
            .replace("<h4>", "<b>")
            .replace("</h4>", "</b>")
            .replace("<h3>", "\n<size=110%><b>")
            .replace("</h3>", "</b></size>\n")
            .replace("<h2>", "\n\n<size=125%><b>")
            .replace("</h2>", "</b></size>\n\n")
            .replace("<h1>", "\n\n<size=150%><b>")
            .replace("</h1>", "</b></size>\n\n")
        )
        body_html = re.sub(r'<a\s+href="[^"]*">(.*?)</a>', r'\1', body_html)

        for _ in range(10):
            body_html = re.sub(r'\n\s*\n\s*\n+', '\n\n', body_html)
            body_html = re.sub(r'^\s*-\s*$\n?', '', body_html, flags=re.MULTILINE)

        dt = datetime.strptime(date, "%Y-%m-%dT%H:%M:%SZ")
        formatted_date = dt.strftime("%Y-%m-%dT00:00:00Z")

        mod_news = {
            "Number": 100000 + i,
            "Title": title,
            "SubTitle": subtitle,
            "ShortTitle": short_title,
            "Text": body_html,
            "Date": formatted_date
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
