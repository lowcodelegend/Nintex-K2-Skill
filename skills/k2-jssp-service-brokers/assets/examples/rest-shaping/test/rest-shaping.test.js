const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const test = require("node:test");
const assert = require("node:assert/strict");

function load(responses = {}) {
  let schema;
  let result;
  const requests = [];
  class XHR {
    open(method, url) { this.method = method; this.url = url; }
    setRequestHeader(name, value) { this.headers = { ...(this.headers || {}), [name]: value }; }
    send() {
      requests.push({ method: this.method, url: this.url, headers: this.headers });
      queueMicrotask(() => {
        this.readyState = 4;
        this.status = 200;
        this.responseText = JSON.stringify(responses[this.url]);
        this.onreadystatechange();
      });
    }
  }
  const context = {
    XMLHttpRequest: XHR,
    postSchema: value => { schema = value; },
    postResult: value => { result = value; },
    console,
    Promise,
    Number,
    String,
    Boolean,
    Array,
    JSON,
    encodeURIComponent
  };
  vm.createContext(context);
  vm.runInContext(fs.readFileSync(path.join(__dirname, "..", "src", "index.js"), "utf8"), context);
  return { context, getSchema: () => schema, getResult: () => result, requests };
}

test("describes exactly three flat example objects", async () => {
  const broker = load();
  await broker.context.ondescribe({});
  assert.deepEqual(Object.keys(broker.getSchema().objects), ["UserSummary", "PostSummary", "TodoSummary"]);
  for (const object of Object.values(broker.getSchema().objects))
    for (const property of Object.values(object.properties))
      assert.match(property.type, /^(string|number|boolean)$/);
});

test("flattens nested user fields", async () => {
  const url = "https://jsonplaceholder.typicode.com/users/1";
  const broker = load({ [url]: { id: 1, name: "Ada", email: "ada@example.test", address: { city: "Dubai" }, company: { name: "Analytical Engines" } } });
  await broker.context.onexecute({ objectName: "UserSummary", methodName: "Read", properties: { Id: 1 } });
  assert.deepEqual(JSON.parse(JSON.stringify(broker.getResult())), {
    Id: 1, Name: "Ada", Email: "ada@example.test", City: "Dubai", CompanyName: "Analytical Engines"
  });
  assert.equal(broker.requests[0].url, url);
});

test("calculates post excerpt and todo status", async () => {
  const postUrl = "https://jsonplaceholder.typicode.com/posts/2";
  const todoUrl = "https://jsonplaceholder.typicode.com/todos/3";
  const broker = load({
    [postUrl]: { id: 2, userId: 9, title: "Post", body: "  one   two  " },
    [todoUrl]: { id: 3, userId: 9, title: "Todo", completed: true }
  });
  await broker.context.onexecute({ objectName: "PostSummary", methodName: "Read", properties: { Id: 2 } });
  assert.equal(broker.getResult().Excerpt, "one two");
  await broker.context.onexecute({ objectName: "TodoSummary", methodName: "Read", properties: { Id: 3 } });
  assert.equal(broker.getResult().Status, "Complete");
});

test("rejects invalid IDs before sending HTTP", async () => {
  const broker = load();
  await assert.rejects(
    broker.context.onexecute({ objectName: "UserSummary", methodName: "Read", properties: { Id: "../2" } }),
    /positive integer/
  );
  assert.equal(broker.requests.length, 0);
});
