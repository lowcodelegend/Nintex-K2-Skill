metadata = {
  systemName: "K2Skills.Examples.RestShaping",
  displayName: "K2 Skills REST Shaping Examples",
  description: "Flattens nested JSONPlaceholder payloads into stable K2 Service Objects."
};

var BASE_URL = "https://jsonplaceholder.typicode.com";

ondescribe = async function () {
  postSchema({
    objects: {
      UserSummary: {
        displayName: "JSSP User Summary",
        description: "Flattens address and company fields from a nested user payload.",
        properties: {
          Id: { displayName: "ID", type: "number" },
          Name: { displayName: "Name", type: "string" },
          Email: { displayName: "Email", type: "string" },
          City: { displayName: "City", type: "string" },
          CompanyName: { displayName: "Company", type: "string" }
        },
        methods: {
          List: { displayName: "List Users", type: "list", outputs: ["Id", "Name", "Email", "City", "CompanyName"] },
          Read: { displayName: "Read User", type: "read", inputs: ["Id"], requiredInputs: ["Id"], outputs: ["Id", "Name", "Email", "City", "CompanyName"] }
        }
      },
      PostSummary: {
        displayName: "JSSP Post Summary",
        description: "Normalizes posts and adds a calculated excerpt.",
        properties: {
          Id: { displayName: "ID", type: "number" },
          UserId: { displayName: "User ID", type: "number" },
          Title: { displayName: "Title", type: "string" },
          Excerpt: { displayName: "Excerpt", type: "string" }
        },
        methods: {
          List: { displayName: "List Posts", type: "list", outputs: ["Id", "UserId", "Title", "Excerpt"] },
          Read: { displayName: "Read Post", type: "read", inputs: ["Id"], requiredInputs: ["Id"], outputs: ["Id", "UserId", "Title", "Excerpt"] }
        }
      },
      TodoSummary: {
        displayName: "JSSP Todo Summary",
        description: "Maps an upstream boolean into a stable status label.",
        properties: {
          Id: { displayName: "ID", type: "number" },
          UserId: { displayName: "User ID", type: "number" },
          Title: { displayName: "Title", type: "string" },
          Completed: { displayName: "Completed", type: "boolean" },
          Status: { displayName: "Status", type: "string" }
        },
        methods: {
          List: { displayName: "List Todos", type: "list", outputs: ["Id", "UserId", "Title", "Completed", "Status"] },
          Read: { displayName: "Read Todo", type: "read", inputs: ["Id"], requiredInputs: ["Id"], outputs: ["Id", "UserId", "Title", "Completed", "Status"] }
        }
      }
    }
  });
};

onexecute = async function (context) {
  var routes = {
    UserSummary: { path: "users", map: mapUser },
    PostSummary: { path: "posts", map: mapPost },
    TodoSummary: { path: "todos", map: mapTodo }
  };
  var route = routes[context.objectName];
  if (!route) throw new Error("Unsupported object: " + context.objectName);
  if (context.methodName !== "List" && context.methodName !== "Read")
    throw new Error("Unsupported method: " + context.methodName);

  var url = BASE_URL + "/" + route.path;
  if (context.methodName === "Read") {
    var id = requirePositiveInteger(context.properties.Id, "Id");
    url += "/" + encodeURIComponent(String(id));
  }
  var payload = await getJson(url);
  if (Array.isArray(payload)) postResult(payload.map(route.map));
  else postResult(route.map(payload));
};

function requirePositiveInteger(value, name) {
  var number = Number(value);
  if (!Number.isInteger(number) || number < 1) throw new Error(name + " must be a positive integer.");
  return number;
}

function text(value) { return value == null ? "" : String(value); }
function excerpt(value) {
  var normalized = text(value).replace(/\s+/g, " ").trim();
  return normalized.length > 80 ? normalized.slice(0, 77) + "..." : normalized;
}
function mapUser(value) {
  return {
    Id: Number(value.id), Name: text(value.name), Email: text(value.email),
    City: text(value.address && value.address.city),
    CompanyName: text(value.company && value.company.name)
  };
}
function mapPost(value) {
  return { Id: Number(value.id), UserId: Number(value.userId), Title: text(value.title), Excerpt: excerpt(value.body) };
}
function mapTodo(value) {
  return {
    Id: Number(value.id), UserId: Number(value.userId), Title: text(value.title),
    Completed: Boolean(value.completed), Status: value.completed ? "Complete" : "Open"
  };
}

function getJson(url) {
  return new Promise(function (resolve, reject) {
    var xhr = new XMLHttpRequest();
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      try {
        if (xhr.status < 200 || xhr.status >= 300) throw new Error("HTTP request failed with status " + xhr.status + ".");
        resolve(JSON.parse(xhr.responseText));
      } catch (error) { reject(error); }
    };
    xhr.open("GET", url);
    xhr.setRequestHeader("Accept", "application/json");
    xhr.send();
  });
}
